namespace StoryCups.Domain.Loyalty;

/// <summary>
/// Баланс на клиент: печати към следващата награда и налични награди. Не се пази, а се извежда от ledger-а.
/// </summary>
public readonly record struct LoyaltyBalance(int Stamps, int Rewards)
{
    public static LoyaltyBalance Zero => default;

    public LoyaltyBalance Apply(IEnumerable<LedgerEntryDraft> entries)
    {
        var stamps = Stamps;
        var rewards = Rewards;
        foreach (var entry in entries)
        {
            stamps += entry.StampDelta;
            rewards += entry.RewardDelta;
        }

        return new LoyaltyBalance(stamps, rewards);
    }
}
