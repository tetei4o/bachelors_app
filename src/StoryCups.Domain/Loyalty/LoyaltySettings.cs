namespace StoryCups.Domain.Loyalty;

/// <summary>
/// Правилата на програмата за лоялност. Пазят се в един ред в базата, не в кода (NFR-11):
/// променят се без нова версия на приложението.
/// </summary>
public sealed class LoyaltySettings
{
    /// <summary>Таблицата има точно един ред с този ключ.</summary>
    public const int SingletonId = 1;

    public const int DefaultStampThreshold = 8;

    public const int DefaultMaxStampsPerTransaction = 4;

    public const int DefaultUndoWindowMinutes = 5;

    private LoyaltySettings()
    {
    }

    public int Id { get; private set; } = SingletonId;

    /// <summary>Колко печата се разменят за една награда (A7).</summary>
    public int StampThreshold { get; private set; }

    /// <summary>
    /// Най-много печати от една покупка (A6). Пречи на трупането на печати от чужди покупки.
    /// </summary>
    public int MaxStampsPerTransaction { get; private set; }

    /// <summary>
    /// Срок на валидност на печатите в дни; празно означава „без срок“ (A8). Изтичането още не се прилага.
    /// </summary>
    public int? StampValidityDays { get; private set; }

    /// <summary>Колко минути след действие то може да се отмени (A18).</summary>
    public int UndoWindowMinutes { get; private set; }

    public TimeSpan UndoWindow => TimeSpan.FromMinutes(UndoWindowMinutes);

    public static LoyaltySettings Default() =>
        Create(DefaultStampThreshold, DefaultMaxStampsPerTransaction, DefaultUndoWindowMinutes);

    public static LoyaltySettings Create(
        int stampThreshold, int maxStampsPerTransaction, int undoWindowMinutes, int? stampValidityDays = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stampThreshold, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxStampsPerTransaction, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(undoWindowMinutes);
        if (stampValidityDays is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(stampValidityDays.Value, 1);
        }

        return new LoyaltySettings
        {
            StampThreshold = stampThreshold,
            MaxStampsPerTransaction = maxStampsPerTransaction,
            UndoWindowMinutes = undoWindowMinutes,
            StampValidityDays = stampValidityDays,
        };
    }
}
