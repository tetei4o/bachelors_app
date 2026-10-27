using Microsoft.AspNetCore.Components;

namespace StoryCups.Web.Components.Account;

/// <summary>
/// Пренасочвания от статичните страници за акаунта (по шаблона на ASP.NET Core Identity).
/// </summary>
internal sealed class IdentityRedirectManager(NavigationManager navigationManager)
{
    public const string StatusCookieName = "Identity.StatusMessage";

    private static readonly CookieBuilder StatusCookieBuilder = new()
    {
        SameSite = SameSiteMode.Strict,
        HttpOnly = true,
        IsEssential = true,
        MaxAge = TimeSpan.FromSeconds(5),
    };

    public void RedirectTo(string? uri)
    {
        uri ??= "";

        // Само локални адреси: защита от open redirect чрез returnUrl.
        if (!Uri.IsWellFormedUriString(uri, UriKind.Relative)
            || uri.StartsWith("//", StringComparison.Ordinal)
            || uri.StartsWith("/\\", StringComparison.Ordinal))
        {
            uri = navigationManager.ToBaseRelativePath(uri);
        }

        navigationManager.NavigateTo(uri);
    }

    public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
    {
        var uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
        var newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
        RedirectTo(newUri);
    }

    public void RedirectToWithStatus(string uri, string message, HttpContext context)
    {
        context.Response.Cookies.Append(StatusCookieName, message, StatusCookieBuilder.Build(context));
        RedirectTo(uri);
    }
}
