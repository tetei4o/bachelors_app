namespace StoryCups.Domain.Loyalty;

/// <summary>
/// Вид на действие. В базата се пази като текст.
/// </summary>
public enum LoyaltyActionType
{
    Purchase = 1,
    RewardRedemption = 2,
    Reversal = 3,
}

/// <summary>
/// Едно действие върху клиентска сметка: покупка, използвана награда или отмяна. Действието е една транзакция и
/// групира записите си в ledger-а. Пази идемпотентния ключ и баланса след действието, за да може повторена заявка
/// да получи същия отговор без второ начисляване (ADR-008). Действията не се променят и не се изтриват.
/// </summary>
public sealed class LoyaltyAction
{
    public const int ReasonMaxLength = 200;

    private readonly List<LedgerEntry> entries = [];

    private LoyaltyAction()
    {
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    /// <summary>Пореден номер на действието в сметката: 1, 2, 3 и така нататък, без повторения.</summary>
    public int Sequence { get; private set; }

    public LoyaltyActionType Type { get; private set; }

    /// <summary>Баристата или администраторът, извършил действието.</summary>
    public Guid ActorId { get; private set; }

    /// <summary>Сесията на сканиране, в която е направено действието. Отмяната няма сесия.</summary>
    public Guid? ScanSessionId { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    /// <summary>SHA-256 на съдържанието на заявката: същият ключ с друго съдържание се отхвърля.</summary>
    public string RequestHash { get; private set; } = string.Empty;

    public Guid? ReversesActionId { get; private set; }

    /// <summary>Причината за отмяната, въведена от служителя.</summary>
    public string? Reason { get; private set; }

    public int StampsAfter { get; private set; }

    public int RewardsAfter { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<LedgerEntry> Entries => entries;

    public LoyaltyBalance BalanceAfter => new(StampsAfter, RewardsAfter);

    /// <summary>
    /// Записва действие в сметката. Взима следващия пореден номер от сметката, което променя нейния ред и така
    /// задейства проверката за едновременни действия.
    /// </summary>
    public static LoyaltyAction Record(
        CustomerAccount account,
        LoyaltyActionType type,
        Guid actorId,
        Guid idempotencyKey,
        string requestHash,
        RuleOutcome outcome,
        DateTimeOffset now,
        Guid? scanSessionId = null,
        Guid? reversesActionId = null,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        if (outcome.Entries.Count == 0)
        {
            throw new ArgumentException("Действие без записи не променя нищо и не се записва.", nameof(outcome));
        }

        if (type == LoyaltyActionType.Reversal)
        {
            ArgumentNullException.ThrowIfNull(reversesActionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, ReasonMaxLength);
        }
        else if (reversesActionId is not null || reason is not null || scanSessionId is null)
        {
            throw new ArgumentException("Покупка и награда се записват в сесия на сканиране, без причина и без отменяно действие.");
        }

        var action = new LoyaltyAction
        {
            Id = Guid.CreateVersion7(now),
            AccountId = account.Id,
            Sequence = account.NextSequence(),
            Type = type,
            ActorId = actorId,
            ScanSessionId = scanSessionId,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            ReversesActionId = reversesActionId,
            Reason = reason,
            StampsAfter = outcome.After.Stamps,
            RewardsAfter = outcome.After.Rewards,
            CreatedAt = now,
        };
        action.entries.AddRange(outcome.Entries.Select(
            (draft, index) => LedgerEntry.From(draft, account.Id, action.Id, index + 1, now)));
        return action;
    }
}
