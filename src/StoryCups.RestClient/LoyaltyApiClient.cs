using System.Globalization;
using StoryCups.Contracts;
using StoryCups.Contracts.Common;
using StoryCups.Contracts.Loyalty;

namespace StoryCups.RestClient;

/// <summary>
/// Услугите на общите екрани „Карта“ и „Сканиране“ през REST API-то (ADR-003): реализацията за мобилното
/// приложение. Проектът е обикновена .NET библиотека без MAUI, затова интеграционните тестове го пускат срещу
/// сървъра в паметта.
/// </summary>
public sealed class LoyaltyApiClient(ApiCaller api) : ICustomerCardService, IBaristaService
{
    public Task<ClientResult<QrTokenResponse>> IssueQrTokenAsync(CancellationToken cancellationToken = default) =>
        api.SendAsync<QrTokenResponse>(HttpMethod.Post, ApiRoutes.Qr.Tokens, cancellationToken: cancellationToken);

    public Task<ClientResult<LoyaltyStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default) =>
        api.SendAsync<LoyaltyStatusDto>(HttpMethod.Get, ApiRoutes.Loyalty.MyStatus, cancellationToken: cancellationToken);

    public Task<ClientResult<LoyaltyHistoryDto>> GetHistoryAsync(int? before = null, CancellationToken cancellationToken = default)
    {
        var path = before is null
            ? ApiRoutes.Loyalty.MyHistory
            : string.Create(CultureInfo.InvariantCulture, $"{ApiRoutes.Loyalty.MyHistory}?before={before}");
        return api.SendAsync<LoyaltyHistoryDto>(HttpMethod.Get, path, cancellationToken: cancellationToken);
    }

    public async Task<ClientResult<IReadOnlyList<ProductDto>>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var result = await api.SendAsync<List<ProductDto>>(
            HttpMethod.Get, ApiRoutes.Loyalty.Products, cancellationToken: cancellationToken);
        return result.IsSuccess
            ? ClientResult<IReadOnlyList<ProductDto>>.Success(result.Value!)
            : ClientResult<IReadOnlyList<ProductDto>>.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    public Task<ClientResult<ScanSessionDto>> ScanAsync(string token, CancellationToken cancellationToken = default) =>
        api.SendAsync<ScanSessionDto>(
            HttpMethod.Post, ApiRoutes.Qr.Scans, new ScanRequest { Token = token }, cancellationToken: cancellationToken);

    public Task<ClientResult<LoyaltyActionResultDto>> SubmitActionAsync(
        Guid scanSessionId, LoyaltyActionRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        api.SendAsync<LoyaltyActionResultDto>(
            HttpMethod.Post, ApiRoutes.Loyalty.ActionsFor(scanSessionId), request, idempotencyKey, cancellationToken);

    public Task<ClientResult<LoyaltyActionResultDto>> ReverseAsync(
        Guid actionId, string reason, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        api.SendAsync<LoyaltyActionResultDto>(
            HttpMethod.Post,
            ApiRoutes.Loyalty.ReversalFor(actionId),
            new ReversalRequest { Reason = reason },
            idempotencyKey,
            cancellationToken);
}
