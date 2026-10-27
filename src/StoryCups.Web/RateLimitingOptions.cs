namespace StoryCups.Web;

/// <summary>
/// Ограничение на заявките към /api/v1/auth от един IP адрес (защита от изпробване на пароли).
/// </summary>
internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitsPerMinute { get; set; } = 10;
}
