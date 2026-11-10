using System.Globalization;

namespace StoryCups.Shared.Services;

/// <summary>
/// Дати и часове за екрана във времето на кафенето (София), независимо къде работи сървърът.
/// </summary>
public static class CafeTime
{
    private static readonly TimeZoneInfo Zone = FindZone();
    private static readonly CultureInfo Bulgarian = CultureInfo.GetCultureInfo("bg-BG");

    /// <summary>„10.11.2026, 14:32“</summary>
    public static string Format(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, Zone).ToString("dd.MM.yyyy, HH:mm", Bulgarian);

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Система без база с часови зони: остава местното време на устройството.
            return TimeZoneInfo.Local;
        }
    }
}
