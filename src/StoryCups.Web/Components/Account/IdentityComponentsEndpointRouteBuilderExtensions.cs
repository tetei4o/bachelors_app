using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.Web.Components.Account;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Изходът е отделен POST endpoint: бутонът е във формата на менюто, а не на статична страница.
    /// </summary>
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var accountGroup = endpoints.MapGroup("/Account");

        accountGroup.MapPost("/Logout", async (
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect(IsLocal(returnUrl) ? $"~/{returnUrl}" : "~/");
        }).ExcludeFromDescription();

        return accountGroup;
    }

    private static bool IsLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
        && !returnUrl.StartsWith('/')
        && !returnUrl.StartsWith('\\');
}
