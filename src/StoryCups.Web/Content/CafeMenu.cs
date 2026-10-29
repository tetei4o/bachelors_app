using System.Globalization;

namespace StoryCups.Web.Content;

public sealed record MenuItem(string Name, int VolumeMl, decimal PriceEur);

public sealed record MenuExtra(string Description, decimal PriceEur);

/// <summary>
/// Менюто от снимката на дъската в кафенето (storycups_menu.png). Тук е само за показване;
/// продуктите, които носят печат, се пазят в базата от среза за правилата (ADR-013).
/// </summary>
public static class CafeMenu
{
    private static readonly NumberFormatInfo PriceFormat = new() { NumberDecimalSeparator = "," };

    public static IReadOnlyList<MenuItem> Drinks { get; } =
    [
        new("Single Espresso", 30, 1.50m),
        new("Double Espresso", 60, 1.80m),
        new("Cappuccino", 240, 2.60m),
        new("Flat White", 240, 3.10m),
        new("Cortado", 130, 2.90m),
        new("Latte", 360, 3.10m),
        new("Matcha Latte", 240, 3.20m),
        new("V60", 250, 4.00m),
        new("Cold Brew", 200, 4.50m),
        new("Iced Latte", 360, 3.40m),
        new("Iced Matcha Latte", 360, 3.50m),
        new("Caffè Leccese", 40, 2.30m),
    ];

    public static IReadOnlyList<MenuExtra> Extras { get; } =
    [
        new("Със specialty сорт", 0.70m),
        new("С алтернативно мляко", 0.70m),
    ];

    /// <summary>Цена по българския запис: десетична запетая и знак след сумата („2,60 €“).</summary>
    public static string FormatPrice(decimal eur) => eur.ToString("0.00", PriceFormat) + " €";
}
