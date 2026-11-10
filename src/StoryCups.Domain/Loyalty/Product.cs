namespace StoryCups.Domain.Loyalty;

/// <summary>
/// Продукт от менюто. Флагът <see cref="EarnsStamp"/> е част от правилата: кои продукти носят печат (A5).
/// Продукт не се изтрива, а се изключва, защото записите в ledger-а сочат към него.
/// </summary>
public sealed class Product
{
    public const int NameMaxLength = 100;

    private Product()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int VolumeMl { get; private set; }

    public decimal PriceEur { get; private set; }

    public bool EarnsStamp { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Ред в менюто, както на дъската в кафенето.</summary>
    public int SortOrder { get; private set; }

    public static Product Create(Guid id, string name, int volumeMl, decimal priceEur, bool earnsStamp, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, NameMaxLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(volumeMl, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(priceEur);

        return new Product
        {
            Id = id,
            Name = name.Trim(),
            VolumeMl = volumeMl,
            PriceEur = priceEur,
            EarnsStamp = earnsStamp,
            IsActive = true,
            SortOrder = sortOrder,
        };
    }
}
