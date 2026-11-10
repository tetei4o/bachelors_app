namespace StoryCups.Shared.Services;

/// <summary>
/// Настройки на общите екрани, които зависят от хоста (сайт или приложение).
/// </summary>
public sealed class ClientUiOptions
{
    /// <summary>
    /// Показва съдържанието на QR кода като текст под него. Само за разработка: така кодът се копира
    /// в екрана на баристата на същия компютър, без телефон. В Production е изключено.
    /// </summary>
    public bool ShowQrTokenText { get; init; }
}
