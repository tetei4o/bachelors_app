namespace StoryCups.Domain.Loyalty;

/// <summary>
/// Ред от покупка: продукт, дали носи печат и брой.
/// </summary>
public sealed record PurchaseLine(Guid ProductId, bool EarnsStamp, int Quantity);

/// <summary>
/// Защо правилата отказват действие. Услугата превръща стойността в код на грешка и текст за екрана.
/// </summary>
public enum RuleDenial
{
    /// <summary>Нито един от избраните продукти не носи печат.</summary>
    NothingToRecord = 1,

    NoRewardAvailable = 2,

    /// <summary>Наградата е напитка от продуктите, които носят печат (A7).</summary>
    ProductNotEligible = 3,

    /// <summary>Отмяна на отмяна няма.</summary>
    NotReversible = 4,

    AlreadyReversed = 5,

    /// <summary>Отменя само баристата, направил действието, или администратор (A18).</summary>
    NotActor = 6,

    UndoWindowExpired = 7,

    /// <summary>Отменя се само последното действие по сметката.</summary>
    NotLastAction = 8,
}

/// <summary>
/// Записите, които едно действие добавя в ledger-а, и балансът преди и след тях.
/// </summary>
public sealed record RuleOutcome(IReadOnlyList<LedgerEntryDraft> Entries, LoyaltyBalance Before, LoyaltyBalance After)
{
    public int StampsAdded => Entries.Where(e => e.Kind == LedgerEntryKind.Purchase).Sum(e => e.StampDelta);

    public int RewardsEarned => Entries.Where(e => e.Kind == LedgerEntryKind.RewardEarned).Sum(e => e.RewardDelta);

    public int RewardsRedeemed => -Entries.Where(e => e.Kind == LedgerEntryKind.RewardRedeemed).Sum(e => e.RewardDelta);
}

public readonly record struct RuleResult(RuleOutcome? Outcome, RuleDenial? Denial)
{
    public bool IsAllowed => Outcome is not null;

    public static implicit operator RuleResult(RuleOutcome outcome) => new(outcome, null);

    public static implicit operator RuleResult(RuleDenial denial) => new(null, denial);
}

/// <summary>
/// Обстоятелствата около искане за отмяна, които не са в самото действие.
/// </summary>
/// <param name="LastEffectiveActionId">Последното действие по сметката, което не е отмяна и не е отменено.</param>
public sealed record ReversalContext(
    Guid ActorId, bool ActorIsAdmin, bool AlreadyReversed, Guid? LastEffectiveActionId, DateTimeOffset Now);

/// <summary>
/// Правилата за печати и награди като чисти функции (ADR-015): получават баланса, избора на баристата и
/// настройките и връщат записите за ledger-а. Не четат база и часовник, затова се тестват с xUnit без инфраструктура.
/// Инвариантът след всяко действие е 0 ≤ печати &lt; праг и награди ≥ 0.
/// </summary>
public static class LoyaltyRules
{
    /// <summary>
    /// „Добави покупка“: по един печат за продукт, който носи печат, най-много M на покупка.
    /// При s1 = s + min(k, M) печата се записват n = ⌊s1 / T⌋ награди и остават s1 − n·T печата.
    /// </summary>
    public static RuleResult Purchase(LoyaltyBalance balance, IReadOnlyList<PurchaseLine> lines, LoyaltySettings settings)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(settings);

        var entries = new List<LedgerEntryDraft>();
        foreach (var line in lines)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(line.Quantity, 1);
            if (!line.EarnsStamp)
            {
                continue;
            }

            for (var unit = 0; unit < line.Quantity && entries.Count < settings.MaxStampsPerTransaction; unit++)
            {
                entries.Add(new LedgerEntryDraft(LedgerEntryKind.Purchase, StampDelta: 1, RewardDelta: 0, line.ProductId));
            }
        }

        if (entries.Count == 0)
        {
            return RuleDenial.NothingToRecord;
        }

        var rewards = (balance.Stamps + entries.Count) / settings.StampThreshold;
        for (var i = 0; i < rewards; i++)
        {
            entries.Add(new LedgerEntryDraft(LedgerEntryKind.RewardEarned, StampDelta: -settings.StampThreshold, RewardDelta: 1));
        }

        return new RuleOutcome(entries, balance, balance.Apply(entries));
    }

    /// <summary>
    /// „Използвай награда“: една награда за една напитка, избрана от баристата. Безплатната напитка не носи печат (A7).
    /// </summary>
    public static RuleResult RedeemReward(LoyaltyBalance balance, Guid productId, bool productEarnsStamp)
    {
        if (balance.Rewards < 1)
        {
            return RuleDenial.NoRewardAvailable;
        }

        if (!productEarnsStamp)
        {
            return RuleDenial.ProductNotEligible;
        }

        LedgerEntryDraft[] entries = [new(LedgerEntryKind.RewardRedeemed, StampDelta: 0, RewardDelta: -1, productId)];
        return new RuleOutcome(entries, balance, balance.Apply(entries));
    }

    /// <summary>
    /// Отмяна (A18): по един обратен запис за всеки запис на действието. Първоначалните записи остават.
    /// Отменя се само последното действие по сметката; така балансът се връща точно към състоянието преди него
    /// и инвариантът се запазва (награда, която вече е използвана, не може да изчезне със задна дата).
    /// </summary>
    public static RuleResult Reverse(
        LoyaltyBalance balance, LoyaltyAction action, ReversalContext context, LoyaltySettings settings)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        if (action.Type == LoyaltyActionType.Reversal)
        {
            return RuleDenial.NotReversible;
        }

        if (context.AlreadyReversed)
        {
            return RuleDenial.AlreadyReversed;
        }

        if (!context.ActorIsAdmin && action.ActorId != context.ActorId)
        {
            return RuleDenial.NotActor;
        }

        if (context.Now > action.CreatedAt + settings.UndoWindow)
        {
            return RuleDenial.UndoWindowExpired;
        }

        if (context.LastEffectiveActionId != action.Id)
        {
            return RuleDenial.NotLastAction;
        }

        var entries = action.Entries
            .OrderBy(entry => entry.Line)
            .Select(entry => new LedgerEntryDraft(
                LedgerEntryKind.Reversal, -entry.StampDelta, -entry.RewardDelta, entry.ProductId, entry.Id))
            .ToList();
        var after = balance.Apply(entries);
        if (after.Stamps < 0 || after.Rewards < 0)
        {
            throw new InvalidOperationException("Отмяната на последното действие не може да даде отрицателен баланс.");
        }

        return new RuleOutcome(entries, balance, after);
    }
}
