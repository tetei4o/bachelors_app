using Microsoft.AspNetCore.Http.HttpResults;
using StoryCups.Application.Qr;
using StoryCups.Contracts;
using StoryCups.Contracts.Loyalty;

namespace StoryCups.Web.Api;

/// <summary>
/// QR токен на клиента и сканиране от баристата (ADR-005, ADR-008).
/// </summary>
internal static class QrEndpoints
{
    public const string IssueRateLimitPolicy = "qr-issue";
    public const string ScanRateLimitPolicy = "scan";

    public static IEndpointRouteBuilder MapQrEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Qr.Tokens, IssueAsync)
            .WithTags("QR")
            .RequireAuthorization(ApiAuthorization.CustomerPolicy)
            .RequireRateLimiting(IssueRateLimitPolicy)
            .WithSummary("Нов QR токен на влезлия клиент: 74 байта в base64url, подписан с HMAC-SHA256, еднократен.");

        app.MapPost(ApiRoutes.Qr.Scans, ScanAsync)
            .WithTags("QR")
            .RequireAuthorization(ApiAuthorization.StaffPolicy)
            .RequireRateLimiting(ScanRateLimitPolicy)
            .Validate<ScanRequest>()
            .WithSummary("Сканиране: проверява токена, използва неговия nonce и отваря сесия на сканиране.");

        return app;
    }

    private static async Task<Results<Ok<QrTokenResponse>, ProblemHttpResult, UnauthorizedHttpResult>> IssueAsync(
        HttpContext context, IQrTokenService tokens, CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserId(out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var result = await tokens.IssueAsync(userId, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiProblems.From(result.Error!);
        }

        // Токенът важи секунди и е еднократен: никой посредник не бива да го пази.
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(result.Value!);
    }

    private static async Task<Results<Created<ScanSessionDto>, ProblemHttpResult, UnauthorizedHttpResult>> ScanAsync(
        ScanRequest request, HttpContext context, IScanService scans, CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserId(out var baristaId))
        {
            return TypedResults.Unauthorized();
        }

        var result = await scans.ScanAsync(baristaId, request.Token, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"{ApiRoutes.Qr.Scans}/{result.Value!.Id}", result.Value)
            : ApiProblems.From(result.Error!);
    }
}
