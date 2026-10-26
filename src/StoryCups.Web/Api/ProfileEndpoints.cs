using Microsoft.AspNetCore.Http.HttpResults;
using StoryCups.Application.Profile;
using StoryCups.Contracts;
using StoryCups.Contracts.Profile;
using StoryCups.Infrastructure.Auth;

namespace StoryCups.Web.Api;

internal static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Profile.Me, GetMeAsync)
            .WithTags("Profile")
            .RequireAuthorization(ApiAuthorization.PolicyName)
            .WithSummary("Профилът на влезлия потребител.");

        return app;
    }

    private static async Task<Results<Ok<ProfileDto>, UnauthorizedHttpResult>> GetMeAsync(
        HttpContext context, IUserProfileReader reader, CancellationToken cancellationToken)
    {
        var subject = context.User.FindFirst(AccessTokenClaims.Subject)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var profile = await reader.FindAsync(userId, cancellationToken);
        return profile is null ? TypedResults.Unauthorized() : TypedResults.Ok(profile);
    }
}
