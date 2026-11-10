using System.Globalization;

namespace StoryCups.Web.Content;

public sealed record MenuExtra(string Description, decimal PriceEur);

/// <summary>
/// Добавките от дъската в кафенето (storycups_menu.png) и записът на цените. Напитките са в таблицата Products
/// (ADR-013, ADR-015); добавките не носят печат (A6) и затова остават само текст на началната страница.
/// </summary>
public static class CafeMenu
{
    private static readonly NumberFormatInfo PriceFormat = new() { NumberDecimalSeparator = "," };

    public static IReadOnlyList<MenuExtra> Extras { get; } =
    [
        new("Със specialty сорт", 0.70m),
        new("С алтернативно мляко", 0.70m),
    ];

    /// <summary>Цена по българския запис: десетична запетая и знак след сумата („2,60 €“).</summary>
    public static string FormatPrice(decimal eur) => eur.ToString("0.00", PriceFormat) + " €";
}
