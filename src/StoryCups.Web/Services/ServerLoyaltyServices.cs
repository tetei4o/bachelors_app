using Microsoft.AspNetCore.Components.Authorization;
using StoryCups.Application.Loyalty;
using StoryCups.Application.Qr;
using StoryCups.Contracts.Common;
using StoryCups.Contracts.Loyalty;
using StoryCups.Domain.Identity;
using StoryCups.Web.Api;

namespace StoryCups.Web.Services;

/// <summary>
/// Картата на клиента в сайта: QR токен, баланс и история направо от Application слоя.
/// Мобилното приложение има своя реализация през /api/v1.
/// </summary>
internal sealed class ServerCustomerCardService(
    AuthenticationStateProvider authenticationStateProvider,
    IServiceScopeFactory scopeFactory,
    SiteRateLimiter rateLimiter)
    : ServerClientService(authenticationStateProvider, scopeFactory, rateLimiter), ICustomerCardService
{
    private const int HistoryPageSize = 20;

    private static readonly string[] Roles = [AppRoles.Customer];

    public Task<ClientResult<QrTokenResponse>> IssueQrTokenAsync(CancellationToken cancellationToken = default) =>
        RunAsync(Roles, QrEndpoints.IssueRateLimitPolicy, async (services, userId, _) =>
            From(await services.GetRequiredService<IQrTokenService>().IssueAsync(userId, cancellationToken)));

    public Task<ClientResult<LoyaltyStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default) =>
        RunAsync(Roles, rateLimitPolicy: null, async (services, userId, _) =>
            ClientResult<LoyaltyStatusDto>.Success(
                await services.GetRequiredService<ILoyaltyReader>().GetStatusAsync(userId, cancellationToken)));

    public Task<ClientResult<LoyaltyHistoryDto>> GetHistoryAsync(int? before = null, CancellationToken cancellationToken = default) =>
        RunAsync(Roles, rateLimitPolicy: null, async (services, userId, _) =>
            ClientResult<LoyaltyHistoryDto>.Success(
                await services.GetRequiredService<ILoyaltyReader>().GetHistoryAsync(userId, HistoryPageSize, before, cancellationToken)));
}

/// <summary>
/// Действията на баристата в сайта. Правата и лимитите са същите като в API-то: роля Бариста или Администратор,
/// проверена тук при всяко извикване.
/// </summary>
internal sealed class ServerBaristaService(
    AuthenticationStateProvider authenticationStateProvider,
    IServiceScopeFactory scopeFactory,
    SiteRateLimiter rateLimiter)
    : ServerClientService(authenticationStateProvider, scopeFactory, rateLimiter), IBaristaService
{
    private static readonly string[] Roles = [AppRoles.Barista, AppRoles.Admin];

    public Task<ClientResult<IReadOnlyList<ProductDto>>> GetProductsAsync(CancellationToken cancellationToken = default) =>
        RunAsync(Roles, rateLimitPolicy: null, async (services, _, _) =>
            ClientResult<IReadOnlyList<ProductDto>>.Success(
                await services.GetRequiredService<IProductCatalog>().GetActiveAsync(cancellationToken)));

    public Task<ClientResult<ScanSessionDto>> ScanAsync(string token, CancellationToken cancellationToken = default) =>
        RunAsync(Roles, QrEndpoints.ScanRateLimitPolicy, async (services, userId, _) =>
            From(await services.GetRequiredService<IScanService>().ScanAsync(userId, token, cancellationToken)));

    public Task<ClientResult<LoyaltyActionResultDto>> SubmitActionAsync(
        Guid scanSessionId, LoyaltyActionRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        RunAsync(Roles, LoyaltyEndpoints.ActionRateLimitPolicy, async (services, userId, _) =>
            Unwrap(await services.GetRequiredService<ILoyaltyActionService>()
                .SubmitAsync(userId, scanSessionId, request, idempotencyKey, cancellationToken)));

    public Task<ClientResult<LoyaltyActionResultDto>> ReverseAsync(
        Guid actionId, string reason, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        RunAsync(Roles, LoyaltyEndpoints.ActionRateLimitPolicy, async (services, userId, user) =>
            Unwrap(await services.GetRequiredService<ILoyaltyActionService>()
                .ReverseAsync(userId, user.IsInRole(AppRoles.Admin), actionId, reason, idempotencyKey, cancellationToken)));

    private static ClientResult<LoyaltyActionResultDto> Unwrap(Application.Common.Result<LoyaltyActionOutcome> result) =>
        result.IsSuccess
            ? ClientResult<LoyaltyActionResultDto>.Success(result.Value!.Result)
            : ClientResult<LoyaltyActionResultDto>.Failure(result.Error!.Code, result.Error.Message);
}
