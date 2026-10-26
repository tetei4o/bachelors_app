using Microsoft.AspNetCore.Http.HttpResults;
using StoryCups.Application.Auth;
using StoryCups.Contracts;
using StoryCups.Contracts.Auth;

namespace StoryCups.Web.Api;

/// <summary>
/// REST API за вход от мобилното приложение (ADR-004).
/// </summary>
internal static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Auth.Group)
            .WithTags("Auth")
            .RequireRateLimiting(RateLimitPolicy)
            .AllowAnonymous();

        group.MapPost("/register", RegisterAsync)
            .Validate<RegisterRequest>()
            .WithSummary("Регистрация на клиент. Отговорът е еднакъв и за вече използван имейл.");

        group.MapPost("/login", LoginAsync)
            .Validate<LoginRequest>()
            .WithSummary("Вход: access токен (15 мин.) и refresh токен (30 дни).");

        group.MapPost("/refresh", RefreshAsync)
            .Validate<RefreshRequest>()
            .WithSummary("Ротация: старият refresh токен става невалиден. Повторната му употреба отнема веригата.");

        group.MapPost("/logout", LogoutAsync)
            .Validate<RefreshRequest>()
            .WithSummary("Отнема refresh токена.");

        return app;
    }

    private static async Task<Results<Accepted<RegistrationAcceptedResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterRequest request, IAuthService auth, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiProblems.From(result.Error!);
        }

        return TypedResults.Accepted(
            (string?)null,
            new RegistrationAcceptedResponse("Изпратихме писмо за потвърждение. Отворете връзката в него, за да активирате акаунта."));
    }

    private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request, IAuthService auth, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value!)
            : ApiProblems.From(result.Error!);
    }

    private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> RefreshAsync(
        RefreshRequest request, IAuthService auth, CancellationToken cancellationToken)
    {
        var result = await auth.RefreshAsync(request.RefreshToken, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value!)
            : ApiProblems.From(result.Error!);
    }

    private static async Task<NoContent> LogoutAsync(
        RefreshRequest request, IAuthService auth, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(request.RefreshToken, cancellationToken);
        return TypedResults.NoContent();
    }
}
