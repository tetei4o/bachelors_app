using System.ComponentModel.DataAnnotations;

namespace StoryCups.Infrastructure.Email;

/// <summary>
/// Публичният адрес на сайта. Връзките в писмата се строят от него, а не от заглавката Host на заявката,
/// за да не може чужд Host да подмени връзката за нова парола.
/// </summary>
public sealed class AppUrlOptions
{
    public const string SectionName = "App";

    [Required(ErrorMessage = "Липсва App:PublicBaseUrl, например https://localhost:7060/.")]
    public string PublicBaseUrl { get; set; } = string.Empty;

    public Uri GetBaseUri()
    {
        var value = PublicBaseUrl.EndsWith('/') ? PublicBaseUrl : PublicBaseUrl + "/";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("App:PublicBaseUrl трябва да е абсолютен http(s) адрес.");
        }

        return uri;
    }
}
