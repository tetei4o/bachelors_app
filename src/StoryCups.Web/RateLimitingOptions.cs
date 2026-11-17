namespace StoryCups.Web;

/// <summary>
/// Ограничения на честотата на заявките към API-то и към формите за акаунта в сайта (NFR-02).
/// </summary>
internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Заявки към /api/v1/auth от един IP адрес (защита от изпробване на пароли).</summary>
    public int AuthPermitsPerMinute { get; set; } = 10;

    /// <summary>
    /// Изпратени форми (POST) от страниците за акаунта в сайта от един IP адрес, общо за вход, регистрация,
    /// забравена парола, нова парола и ново писмо за потвърждение. Лимитът е по-висок от този за API-то:
    /// клиентите в кафенето излизат през един адрес, а форма с грешка се изпраща повторно.
    /// </summary>
    public int AccountFormPostsPerMinute { get; set; } = 20;

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
