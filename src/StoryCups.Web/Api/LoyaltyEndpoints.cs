using Microsoft.AspNetCore.Http.HttpResults;
using StoryCups.Application.Common;
using StoryCups.Application.Loyalty;
using StoryCups.Contracts;
using StoryCups.Contracts.Loyalty;
using StoryCups.Domain.Identity;

namespace StoryCups.Web.Api;

/// <summary>
/// Печати и награди (ADR-008, ADR-015): действията на баристата в сесия на сканиране и данните на клиента.
/// Това са записи за покупки на място в кафенето; през API-то не се поръчва и не се плаща.
/// </summary>
internal static class LoyaltyEndpoints
{
    public const string ActionRateLimitPolicy = "loyalty-action";

    private const int DefaultHistoryPageSize = 20;

    public static IEndpointRouteBuilder MapLoyaltyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Loyalty.Products, GetProductsAsync)
            .WithTags("Loyalty")
            .RequireAuthorization(ApiAuthorization.StaffPolicy)
            .WithSummary("Активните продукти от менюто, от които баристата избира.");

        app.MapPost(ApiRoutes.Loyalty.ScanActions, SubmitActionAsync)
            .WithTags("Loyalty")
            .RequireAuthorization(ApiAuthorization.StaffPolicy)
            .RequireRateLimiting(ActionRateLimitPolicy)
            .Validate<LoyaltyActionRequest>()
            .WithSummary("Действие в сесия на сканиране: начисляване на печати за покупка или използване на награда. Изисква Idempotency-Key.");

        app.MapPost(ApiRoutes.Loyalty.ActionReversal, ReverseActionAsync)
            .WithTags("Loyalty")
            .RequireAuthorization(ApiAuthorization.StaffPolicy)
            .RequireRateLimiting(ActionRateLimitPolicy)
            .Validate<ReversalRequest>()
            .WithSummary("Отмяна на последното действие по сметката с обратни записи. Изисква Idempotency-Key.");

        app.MapGet(ApiRoutes.Loyalty.MyStatus, GetMyStatusAsync)
            .WithTags("Loyalty")
            .RequireAuthorization(ApiAuthorization.CustomerPolicy)
            .WithSummary("Печатите и наградите на влезлия клиент.");

        app.MapGet(ApiRoutes.Loyalty.MyHistory, GetMyHistoryAsync)
            .WithTags("Loyalty")
            .RequireAuthorization(ApiAuthorization.CustomerPolicy)
            .WithSummary("Историята на влезлия клиент, от най-новото действие назад.");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<ProductDto>>> GetProductsAsync(
        IProductCatalog catalog, CancellationToken cancellationToken) =>
        TypedResults.Ok(await catalog.GetActiveAsync(cancellationToken));

    private static async Task<Results<Ok<LoyaltyActionResultDto>, ProblemHttpResult, UnauthorizedHttpResult>> SubmitActionAsync(
        Guid scanId,
        LoyaltyActionRequest request,
        HttpContext context,
        ILoyaltyActionService actions,
        CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserId(out var actorId))
        {
            return TypedResults.Unauthorized();
        }

        if (!TryGetIdempotencyKey(context, out var idempotencyKey))
        {
            return MissingIdempotencyKey();
        }

        var result = await actions.SubmitAsync(actorId, scanId, request, idempotencyKey, cancellationToken);
        return ToHttp(result, context);
    }

    private static async Task<Results<Ok<LoyaltyActionResultDto>, ProblemHttpResult, UnauthorizedHttpResult>> ReverseActionAsync(
        Guid actionId,
        ReversalRequest request,
        HttpContext context,
        ILoyaltyActionService actions,
        CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserId(out var actorId))
        {
            return TypedResults.Unauthorized();
        }

        if (!TryGetIdempotencyKey(context, out var idempotencyKey))
        {
            return MissingIdempotencyKey();
        }

        var result = await actions.ReverseAsync(
            actorId, context.User.IsInRole(AppRoles.Admin), actionId, request.Reason, idempotencyKey, cancellationToken);
        return ToHttp(result, context);
    }

    private static async Task<Results<Ok<LoyaltyStatusDto>, UnauthorizedHttpResult>> GetMyStatusAsync(
        HttpContext context, ILoyaltyReader reader, CancellationToken cancellationToken) =>
        context.User.TryGetUserId(out var userId)
            ? TypedResults.Ok(await reader.GetStatusAsync(userId, cancellationToken))
            : TypedResults.Unauthorized();

    private static async Task<Results<Ok<LoyaltyHistoryDto>, UnauthorizedHttpResult>> GetMyHistoryAsync(
        HttpContext context, ILoyaltyReader reader, int? limit, int? before, CancellationToken cancellationToken) =>
        context.User.TryGetUserId(out var userId)
            ? TypedResults.Ok(await reader.GetHistoryAsync(userId, limit ?? DefaultHistoryPageSize, before, cancellationToken))
            : TypedResults.Unauthorized();

    private static Results<Ok<LoyaltyActionResultDto>, ProblemHttpResult, UnauthorizedHttpResult> ToHttp(
        Result<LoyaltyActionOutcome> result, HttpContext context)
    {
        if (!result.IsSuccess)
        {
            return ApiProblems.From(result.Error!);
        }

        if (result.Value!.Replayed)
        {
            context.Response.Headers[ApiHeaders.IdempotentReplayed] = "true";
        }

        return TypedResults.Ok(result.Value.Result);
    }

    /// <summary>
    /// Ключът е UUID, създаден от клиента за едно намерение на потребителя, и се праща отново при повторен опит.
    /// </summary>
    private static bool TryGetIdempotencyKey(HttpContext context, out Guid idempotencyKey)
    {
        idempotencyKey = Guid.Empty;
        var values = context.Request.Headers[ApiHeaders.IdempotencyKey];
        return values.Count == 1 && Guid.TryParse(values[0], out idempotencyKey) && idempotencyKey != Guid.Empty;
    }

    private static ProblemHttpResult MissingIdempotencyKey() => ApiProblems.From(new Error(
        LoyaltyErrorCodes.IdempotencyKeyRequired,
        $"Заявката изисква заглавие {ApiHeaders.IdempotencyKey} с UUID."));
}
