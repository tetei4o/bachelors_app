namespace StoryCups.Web;

/// <summary>
/// Ограничения на честотата на заявките към API-то (NFR-02).
/// </summary>
internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Заявки към /api/v1/auth от един IP адрес (защита от изпробване на пароли).</summary>
    public int AuthPermitsPerMinute { get; set; } = 10;

    /// <summary>
    /// Нови QR токени на един клиент. При отворен екран приложението иска токен на около 40 s (ADR-006).
    /// </summary>
    public int QrTokensPerMinute { get; set; } = 20;

    /// <summary>
    /// Сканирания на един бариста. Ограничава и изпробването на подправени кодове.
    /// </summary>
    public int ScansPerMinute { get; set; } = 60;

    /// <summary>
    /// Действия (начисляване, награда, отмяна) на един бариста.
    /// </summary>
    public int ActionsPerMinute { get; set; } = 120;
}
