using StoryCups.Application.Common;
using StoryCups.Contracts.Loyalty;

namespace StoryCups.Application.Qr;

/// <summary>
/// Сканиране на QR токен от баристата (ADR-005, ADR-008): проверка на подписа и срока, еднократно използване
/// на nonce и отваряне на сесия на сканиране.
/// </summary>
public interface IScanService
{
    Task<Result<ScanSessionDto>> ScanAsync(Guid baristaId, string token, CancellationToken cancellationToken = default);
}
