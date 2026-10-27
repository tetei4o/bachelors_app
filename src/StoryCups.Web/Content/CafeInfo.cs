namespace StoryCups.Web.Content;

public sealed record OpeningHours(string Days, string Hours);

/// <summary>
/// Факти за кафенето, показвани в публичната част (ADR-013). Само проверени данни;
/// липсващите са отбелязани с [ПОПЪЛНИ] и се попълват от автора.
/// </summary>
public static class CafeInfo
{
    public const string Name = "Story Cups Specialty Coffee";

    public const string City = "София";

    public const string InstagramHandle = "storycups.specialtycoffee";

    public const string InstagramUrl = "https://www.instagram.com/storycups.specialtycoffee/";

    public const string MapsUrl = "https://maps.app.goo.gl/rf2qMb3dt7j3bwPT6";

    // Адресът и работното време са от автора (2026-10-27).
    public const string Address = "Цар Иван Асен II 20";

    public static IReadOnlyList<OpeningHours> Hours { get; } =
    [
        new("Понеделник – петък", "7:00–18:00"),
        new("Събота", "9:00–18:00"),
        new("Неделя", "почивен ден"),
    ];

    /// <summary>Кога цените в <see cref="CafeMenu"/> са сверени с менюто в кафенето.</summary>
    public const string MenuCheckedOn = "[ПОПЪЛНИ: дата на проверка]";
}
