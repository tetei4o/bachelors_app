using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StoryCups.Application.Common;
using StoryCups.Application.Qr;
using StoryCups.Contracts.Loyalty;
using StoryCups.Domain.Qr;
using StoryCups.Infrastructure.Loyalty;
using StoryCups.Infrastructure.Persistence;

namespace StoryCups.Infrastructure.Qr;

/// <summary>
/// Издава QR токен на активен клиент (ADR-005). Чете само публичния идентификатор; в базата не се пише нищо.
/// </summary>
public sealed class QrTokenService(
    AppDbContext db,
    QrTokenCodec codec,
    IOptions<QrOptions> options,
    TimeProvider timeProvider) : IQrTokenService
{
    public async Task<Result<QrTokenResponse>> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var publicId = await db.ActiveCustomers(now)
            .Where(user => user.Id == userId)
            .Select(user => (Guid?)user.PublicId)
            .SingleOrDefaultAsync(cancellationToken);
        if (publicId is null)
        {
            return Result<QrTokenResponse>.Failure(
                LoyaltyErrorCodes.CustomerInactive, "QR код се издава само на активен клиентски акаунт.");
        }

        var settings = options.Value;
        var issued = codec.Issue(publicId.Value, now, settings.TokenLifetime);
        var lifetime = settings.TokenLifetime;
        return Result<QrTokenResponse>.Success(new QrTokenResponse(
            issued.Token,
            issued.Payload.ExpiresAt,
            (int)lifetime.TotalSeconds,
            (int)(lifetime - settings.RefreshBeforeExpiry).TotalSeconds));
    }
}
