using StoryCups.Contracts.Common;

namespace StoryCups.Contracts.Loyalty;

/// <summary>
/// Данните за екрана „Карта“ на клиента (ADR-003). Сайтът го реализира към Application слоя,
/// мобилното приложение през REST API-то.
/// </summary>
public interface ICustomerCardService
{
    Task<ClientResult<QrTokenResponse>> IssueQrTokenAsync(CancellationToken cancellationToken = default);

    Task<ClientResult<LoyaltyStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<ClientResult<LoyaltyHistoryDto>> GetHistoryAsync(int? before = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Действията на баристата (ADR-008): сканиране, начисляване на печати, награда и отмяна.
/// Ключът за идемпотентност се създава от екрана и се подава отново при повторен опит.
/// </summary>
public interface IBaristaService
{
    Task<ClientResult<IReadOnlyList<ProductDto>>> GetProductsAsync(CancellationToken cancellationToken = default);

    Task<ClientResult<ScanSessionDto>> ScanAsync(string token, CancellationToken cancellationToken = default);

    Task<ClientResult<LoyaltyActionResultDto>> SubmitActionAsync(
        Guid scanSessionId, LoyaltyActionRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default);

    Task<ClientResult<LoyaltyActionResultDto>> ReverseAsync(
        Guid actionId, string reason, Guid idempotencyKey, CancellationToken cancellationToken = default);
}
