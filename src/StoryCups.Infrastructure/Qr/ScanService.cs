using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoryCups.Application.Common;
using StoryCups.Application.Qr;
using StoryCups.Contracts.Loyalty;
using StoryCups.Contracts.Profile;
using StoryCups.Domain.Qr;
using StoryCups.Infrastructure.Loyalty;
using StoryCups.Infrastructure.Persistence;
using StoryCups.Infrastructure.Persistence.Configurations;

namespace StoryCups.Infrastructure.Qr;

/// <summary>
/// Сканиране на QR токен (ADR-005, ADR-008). Стъпките са в реда от ADR-005: формат, ключ, подпис и срок без база,
/// после клиентът, накрая nonce и сесията в една транзакция.
/// </summary>
public sealed class ScanService(
    AppDbContext db,
    QrTokenCodec codec,
    IOptions<QrOptions> options,
    TimeProvider timeProvider,
    ILogger<ScanService> logger) : IScanService
{
    public async Task<Result<ScanSessionDto>> ScanAsync(Guid baristaId, string token, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var validation = codec.Validate(token, now);
        if (!validation.IsValid)
        {
            // Токенът не се пише в лога: валиден код в лог файл би могъл да се използва.
            logger.LogInformation("Отхвърлен QR код: {Status}.", validation.Status);
            return validation.Status == QrTokenStatus.Expired
                ? Failure(LoyaltyErrorCodes.QrExpired, "Кодът е изтекъл. Помолете клиента да покаже новия код.")
                : Failure(LoyaltyErrorCodes.QrInvalid, "Невалиден код.");
        }

        var payload = validation.Payload!;
        var customer = await db.ActiveCustomers(now)
            .Where(user => user.PublicId == payload.PublicId)
            .Select(user => new { user.Id, user.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return Failure(LoyaltyErrorCodes.CustomerInactive, "Акаунтът на клиента не е активен.");
        }

        if (customer.Id == baristaId)
        {
            return Failure(LoyaltyErrorCodes.SelfScan, "Не можете да сканирате собствения си код.");
        }

        var accountId = await db.EnsureCustomerAccountAsync(customer.Id, now, cancellationToken);

        // Nonce и сесията са в един SaveChanges, тоест в една транзакция: или и двете, или нито едно.
        var session = ScanSession.Open(baristaId, accountId, now, options.Value.ScanSessionLifetime);
        db.UsedQrNonces.Add(UsedQrNonce.From(payload));
        db.ScanSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(UsedQrNonceConfiguration.PrimaryKeyName))
        {
            logger.LogWarning("Повторно сканиран QR код от бариста {BaristaId}.", baristaId);
            return Failure(LoyaltyErrorCodes.QrAlreadyUsed, "Кодът вече е използван. Помолете клиента да покаже новия код.");
        }

        return Result<ScanSessionDto>.Success(new ScanSessionDto(
            session.Id,
            session.ExpiresAt,
            (int)options.Value.ScanSessionLifetime.TotalSeconds,
            DisplayNames.Initials(customer.DisplayName)));
    }

    private static Result<ScanSessionDto> Failure(string code, string message) =>
        Result<ScanSessionDto>.Failure(code, message);
}
