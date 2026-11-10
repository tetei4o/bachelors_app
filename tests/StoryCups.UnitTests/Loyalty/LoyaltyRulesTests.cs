using StoryCups.Domain.Loyalty;

namespace StoryCups.UnitTests.Loyalty;

/// <summary>
/// Правилата за печати и награди като чисти функции (ADR-015), без база и без часовник.
/// </summary>
public sealed class LoyaltyRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly LoyaltySettings Settings = LoyaltySettings.Default();
    private static readonly Guid Cappuccino = Guid.NewGuid();
    private static readonly Guid Latte = Guid.NewGuid();
    private static readonly Guid Barista = Guid.NewGuid();

    [Fact]
    public void Purchase_OneDrink_AddsOneStamp()
    {
        var result = LoyaltyRules.Purchase(LoyaltyBalance.Zero, [new PurchaseLine(Cappuccino, true, 1)], Settings);

        var outcome = result.Outcome!;
        var entry = Assert.Single(outcome.Entries);
        Assert.Equal(new LedgerEntryDraft(LedgerEntryKind.Purchase, 1, 0, Cappuccino), entry);
        Assert.Equal(new LoyaltyBalance(1, 0), outcome.After);
        Assert.Equal(1, outcome.StampsAdded);
        Assert.Equal(0, outcome.RewardsEarned);
    }

    /// <summary>Примерът от раздел 2.4 на тезата: 6 печата и покупка на 3 напитки дават 1 печат и нова награда.</summary>
    [Fact]
    public void Purchase_CrossingTheThreshold_EarnsRewardAndKeepsTheRemainder()
    {
        var before = new LoyaltyBalance(6, 0);

        var outcome = LoyaltyRules.Purchase(before, [new PurchaseLine(Cappuccino, true, 3)], Settings).Outcome!;

        Assert.Equal(new LoyaltyBalance(1, 1), outcome.After);
        Assert.Equal(3, outcome.StampsAdded);
        Assert.Equal(1, outcome.RewardsEarned);
        Assert.Equal(4, outcome.Entries.Count);
        Assert.Equal(new LedgerEntryDraft(LedgerEntryKind.RewardEarned, -8, 1), outcome.Entries[^1]);
    }

    [Fact]
    public void Purchase_ExactlyAtThreshold_LeavesZeroStamps()
    {
        var outcome = LoyaltyRules.Purchase(new LoyaltyBalance(7, 2), [new PurchaseLine(Latte, true, 1)], Settings).Outcome!;

        Assert.Equal(new LoyaltyBalance(0, 3), outcome.After);
    }

    [Fact]
    public void Purchase_IsCappedByMaxStampsPerTransaction()
    {
        var outcome = LoyaltyRules.Purchase(
            LoyaltyBalance.Zero,
            [new PurchaseLine(Cappuccino, true, 3), new PurchaseLine(Latte, true, 6)],
            Settings).Outcome!;

        Assert.Equal(Settings.MaxStampsPerTransaction, outcome.StampsAdded);
        Assert.Equal(new LoyaltyBalance(4, 0), outcome.After);

        // Записите пазят кои напитки са донесли печат: трите Cappuccino и първото Latte.
        Assert.Equal([Cappuccino, Cappuccino, Cappuccino, Latte], outcome.Entries.Select(e => e.ProductId!.Value));
    }

    [Fact]
    public void Purchase_ProductsWithoutStamp_AreNotRecorded()
    {
        var water = Guid.NewGuid();

        var mixed = LoyaltyRules.Purchase(
            LoyaltyBalance.Zero, [new PurchaseLine(water, false, 2), new PurchaseLine(Latte, true, 1)], Settings);
        var onlyWater = LoyaltyRules.Purchase(LoyaltyBalance.Zero, [new PurchaseLine(water, false, 2)], Settings);

        Assert.Equal(1, mixed.Outcome!.StampsAdded);
        Assert.Equal(Latte, Assert.Single(mixed.Outcome.Entries).ProductId);
        Assert.False(onlyWater.IsAllowed);
        Assert.Equal(RuleDenial.NothingToRecord, onlyWater.Denial);
    }

    [Fact]
    public void Purchase_WithLargerCap_CanEarnTwoRewardsAtOnce()
    {
        var settings = LoyaltySettings.Create(stampThreshold: 3, maxStampsPerTransaction: 10, undoWindowMinutes: 5);

        var outcome = LoyaltyRules.Purchase(new LoyaltyBalance(2, 0), [new PurchaseLine(Latte, true, 5)], settings).Outcome!;

        Assert.Equal(2, outcome.RewardsEarned);
        Assert.Equal(new LoyaltyBalance(1, 2), outcome.After);
    }

    /// <summary>
    /// Инвариантът от раздел 2.4: при всяко начално състояние и брой напитки печатите остават под прага,
    /// наградите не намаляват и нищо не се губи: s + min(k, M) = s' + T·(r' − r).
    /// </summary>
    [Fact]
    public void Purchase_KeepsTheInvariant_ForEveryBalanceAndQuantity()
    {
        foreach (var threshold in new[] { 1, 2, 8, 10 })
        {
            foreach (var cap in new[] { 1, 4, 20 })
            {
                var settings = LoyaltySettings.Create(threshold, cap, undoWindowMinutes: 5);
                for (var stamps = 0; stamps < threshold; stamps++)
                {
                    for (var quantity = 1; quantity <= 20; quantity++)
                    {
                        var before = new LoyaltyBalance(stamps, 3);

                        var outcome = LoyaltyRules.Purchase(before, [new PurchaseLine(Latte, true, quantity)], settings).Outcome!;

                        Assert.InRange(outcome.After.Stamps, 0, threshold - 1);
                        Assert.True(outcome.After.Rewards >= before.Rewards);
                        Assert.Equal(
                            stamps + Math.Min(quantity, cap),
                            outcome.After.Stamps + (threshold * (outcome.After.Rewards - before.Rewards)));
                        Assert.Equal(before.Apply(outcome.Entries), outcome.After);
                    }
                }
            }
        }
    }

    [Fact]
    public void Purchase_RejectsNonPositiveQuantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoyaltyRules.Purchase(LoyaltyBalance.Zero, [new PurchaseLine(Latte, true, 0)], Settings));
    }

    [Fact]
    public void RedeemReward_UsesOneRewardAndAddsNoStamp()
    {
        var outcome = LoyaltyRules.RedeemReward(new LoyaltyBalance(3, 2), Latte, productEarnsStamp: true).Outcome!;

        Assert.Equal(new LedgerEntryDraft(LedgerEntryKind.RewardRedeemed, 0, -1, Latte), Assert.Single(outcome.Entries));
        Assert.Equal(new LoyaltyBalance(3, 1), outcome.After);
        Assert.Equal(1, outcome.RewardsRedeemed);
        Assert.Equal(0, outcome.StampsAdded);
    }

    [Fact]
    public void RedeemReward_WithoutReward_IsDenied()
    {
        var result = LoyaltyRules.RedeemReward(new LoyaltyBalance(7, 0), Latte, productEarnsStamp: true);

        Assert.Equal(RuleDenial.NoRewardAvailable, result.Denial);
    }

    [Fact]
    public void RedeemReward_ForProductOutsideTheProgramme_IsDenied()
    {
        var result = LoyaltyRules.RedeemReward(new LoyaltyBalance(0, 1), Guid.NewGuid(), productEarnsStamp: false);

        Assert.Equal(RuleDenial.ProductNotEligible, result.Denial);
    }

    [Fact]
    public void Reverse_LastPurchase_RestoresTheBalanceWithOppositeEntries()
    {
        var account = CustomerAccount.Open(Guid.NewGuid(), Now);
        var before = new LoyaltyBalance(6, 0);
        var purchase = Record(account, LoyaltyRules.Purchase(before, [new PurchaseLine(Cappuccino, true, 3)], Settings).Outcome!);

        var result = LoyaltyRules.Reverse(purchase.BalanceAfter, purchase, Context(purchase.Id), Settings);

        var outcome = result.Outcome!;
        Assert.Equal(before, outcome.After);
        Assert.Equal(purchase.Entries.Count, outcome.Entries.Count);
        Assert.All(outcome.Entries, entry => Assert.Equal(LedgerEntryKind.Reversal, entry.Kind));
        Assert.Equal(purchase.Entries.Select(e => e.Id), outcome.Entries.Select(e => e.ReversesEntryId!.Value));
        Assert.Equal(0, outcome.Entries.Sum(e => e.StampDelta) + purchase.Entries.Sum(e => e.StampDelta));
        Assert.Equal(0, outcome.Entries.Sum(e => e.RewardDelta) + purchase.Entries.Sum(e => e.RewardDelta));
    }

    [Fact]
    public void Reverse_Redemption_ReturnsTheReward()
    {
        var account = CustomerAccount.Open(Guid.NewGuid(), Now);
        var redemption = Record(
            account, LoyaltyRules.RedeemReward(new LoyaltyBalance(2, 1), Latte, true).Outcome!, LoyaltyActionType.RewardRedemption);

        var outcome = LoyaltyRules.Reverse(redemption.BalanceAfter, redemption, Context(redemption.Id), Settings).Outcome!;

        Assert.Equal(new LoyaltyBalance(2, 1), outcome.After);
    }

    [Fact]
    public void Reverse_ByAnotherBarista_IsDenied_ButAdminMay()
    {
        var purchase = NewPurchase();
        var otherBarista = Context(purchase.Id) with { ActorId = Guid.NewGuid() };
        var admin = otherBarista with { ActorIsAdmin = true };

        Assert.Equal(RuleDenial.NotActor, LoyaltyRules.Reverse(purchase.BalanceAfter, purchase, otherBarista, Settings).Denial);
        Assert.True(LoyaltyRules.Reverse(purchase.BalanceAfter, purchase, admin, Settings).IsAllowed);
    }

    [Fact]
    public void Reverse_IsAllowedUntilTheEndOfTheUndoWindow()
    {
        var purchase = NewPurchase();
        var atTheEnd = Context(purchase.Id) with { Now = Now.AddMinutes(5) };
        var afterIt = Context(purchase.Id) with { Now = Now.AddMinutes(5).AddSeconds(1) };

        Assert.True(LoyaltyRules.Reverse(purchase.BalanceAfter, purchase, atTheEnd, Settings).IsAllowed);
        Assert.Equal(RuleDenial.UndoWindowExpired, LoyaltyRules.Reverse(purchase.BalanceAfter, purchase, afterIt, Settings).Denial);
    }

    [Fact]
    public void Reverse_AlreadyReversedAction_IsDenied()
    {
        var purchase = NewPurchase();
        var context = Context(purchase.Id) with { AlreadyReversed = true, LastEffectiveActionId = null };

        Assert.Equal(RuleDenial.AlreadyReversed, LoyaltyRules.Reverse(LoyaltyBalance.Zero, purchase, context, Settings).Denial);
    }

    [Fact]
    public void Reverse_ActionThatIsNotTheLastOne_IsDenied()
    {
        var purchase = NewPurchase();
        var context = Context(lastEffectiveActionId: Guid.NewGuid());

        Assert.Equal(RuleDenial.NotLastAction, LoyaltyRules.Reverse(purchase.BalanceAfter, purchase, context, Settings).Denial);
    }

    [Fact]
    public void Reverse_OfAReversal_IsDenied()
    {
        var account = CustomerAccount.Open(Guid.NewGuid(), Now);
        var purchase = Record(account, LoyaltyRules.Purchase(LoyaltyBalance.Zero, [new PurchaseLine(Latte, true, 1)], Settings).Outcome!);
        var reversalOutcome = LoyaltyRules.Reverse(purchase.BalanceAfter, purchase, Context(purchase.Id), Settings).Outcome!;
        var reversal = LoyaltyAction.Record(
            account, LoyaltyActionType.Reversal, Barista, Guid.NewGuid(), "hash", reversalOutcome, Now,
            reversesActionId: purchase.Id, reason: "грешен клиент");

        var result = LoyaltyRules.Reverse(reversal.BalanceAfter, reversal, Context(reversal.Id), Settings);

        Assert.Equal(RuleDenial.NotReversible, result.Denial);
    }

    private static LoyaltyAction NewPurchase() => Record(
        CustomerAccount.Open(Guid.NewGuid(), Now),
        LoyaltyRules.Purchase(LoyaltyBalance.Zero, [new PurchaseLine(Latte, true, 2)], Settings).Outcome!);

    private static LoyaltyAction Record(
        CustomerAccount account, RuleOutcome outcome, LoyaltyActionType type = LoyaltyActionType.Purchase) =>
        LoyaltyAction.Record(account, type, Barista, Guid.NewGuid(), "hash", outcome, Now, scanSessionId: Guid.NewGuid());

    private static ReversalContext Context(Guid? lastEffectiveActionId) =>
        new(Barista, ActorIsAdmin: false, AlreadyReversed: false, lastEffectiveActionId, Now.AddMinutes(1));
}
