namespace StoryCups.Domain.Loyalty;

/// <summary>
/// Вид на запис в ledger-а. В базата се пази като текст.
/// </summary>
public enum LedgerEntryKind
{
    /// <summary>Купен продукт, който носи печат: +1 печат.</summary>
    Purchase = 1,

    /// <summary>Печатите са стигнали прага: −T печата, +1 награда.</summary>
    RewardEarned = 2,

    /// <summary>Използвана награда: −1 награда; записът пази безплатния продукт.</summary>
    RewardRedeemed = 3,

    /// <summary>Отмяна: обратното на един запис от отмененото действие.</summary>
    Reversal = 5,
}

/// <summary>
/// Запис, който правилата предлагат, преди да стане ред в ledger-а.
/// </summary>
public sealed record LedgerEntryDraft(
    LedgerEntryKind Kind, int StampDelta, int RewardDelta, Guid? ProductId = null, Guid? ReversesEntryId = null);

/// <summary>
/// Един ред в append-only дневника на транзакциите. Редовете само се добавят: няма метод за промяна,
/// а базата отхвърля UPDATE и DELETE върху таблицата (ADR-015). Балансът е сумата на редовете.
/// </summary>
public sealed class LedgerEntry
{
    private LedgerEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid ActionId { get; private set; }

    /// <summary>Пореден номер на записа в действието, от 1: пази реда, в който правилата са го създали.</summary>
    public int Line { get; private set; }

    public LedgerEntryKind Kind { get; private set; }

    public int StampDelta { get; private set; }

    public int RewardDelta { get; private set; }

    public Guid? ProductId { get; private set; }

    /// <summary>За запис от вид Reversal: кой запис обръща.</summary>
    public Guid? ReversesEntryId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static LedgerEntry From(LedgerEntryDraft draft, Guid accountId, Guid actionId, int line, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        AccountId = accountId,
        ActionId = actionId,
        Line = line,
        Kind = draft.Kind,
        StampDelta = draft.StampDelta,
        RewardDelta = draft.RewardDelta,
        ProductId = draft.ProductId,
        ReversesEntryId = draft.ReversesEntryId,
        CreatedAt = now,
    };
}
