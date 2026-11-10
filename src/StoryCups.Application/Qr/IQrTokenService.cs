using StoryCups.Application.Common;
using StoryCups.Contracts.Loyalty;

namespace StoryCups.Application.Qr;

/// <summary>
/// Издава динамичния QR токен на клиента (ADR-005, ADR-006). Издаването не пише в базата.
/// </summary>
public interface IQrTokenService
{
    Task<Result<QrTokenResponse>> IssueAsync(Guid userId, CancellationToken cancellationToken = default);
}
