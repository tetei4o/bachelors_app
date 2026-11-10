namespace StoryCups.Infrastructure.Seeding;

/// <summary>
/// Напитките от менюто на кафенето, предоставено от собственика (A5, ADR-013). Влизат в базата с миграцията.
/// Ключовете са постоянни, за да не се сменят при всяка нова миграция.
/// </summary>
public static class MenuSeed
{
    public static IReadOnlyList<MenuSeedDrink> Drinks { get; } =
    [
        new(Id(1), "Single Espresso", 30, 1.50m, 1),
        new(Id(2), "Double Espresso", 60, 1.80m, 2),
        new(Id(3), "Cappuccino", 240, 2.60m, 3),
        new(Id(4), "Flat White", 240, 3.10m, 4),
        new(Id(5), "Cortado", 130, 2.90m, 5),
        new(Id(6), "Latte", 360, 3.10m, 6),
        new(Id(7), "Matcha Latte", 240, 3.20m, 7),
        new(Id(8), "V60", 250, 4.00m, 8),
        new(Id(9), "Cold Brew", 200, 4.50m, 9),
        new(Id(10), "Iced Latte", 360, 3.40m, 10),
        new(Id(11), "Iced Matcha Latte", 360, 3.50m, 11),
        new(Id(12), "Caffè Leccese", 40, 2.30m, 12),
    ];

    private static Guid Id(int number) => Guid.Parse($"0195f0a0-0000-7000-8000-{0x100 + number:x12}");
}

public sealed record MenuSeedDrink(Guid Id, string Name, int VolumeMl, decimal PriceEur, int SortOrder);
