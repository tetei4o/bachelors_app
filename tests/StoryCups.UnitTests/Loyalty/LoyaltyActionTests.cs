using StoryCups.Domain.Loyalty;

namespace StoryCups.UnitTests.Loyalty;

public sealed class LoyaltyActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly LoyaltySettings Settings = LoyaltySettings.Default();

    [Fact]
    public void Record_TakesTheNextSequenceOfTheAccount_AndCopiesTheOutcome()
    {
        var account = CustomerAccount.Open(Guid.NewGuid(), Now);
        var actor = Guid.NewGuid();
        var session = Guid.NewGuid();
        var key = Guid.NewGuid();
        var product = Guid.NewGuid();
        var outcome = LoyaltyRules.Purchase(new LoyaltyBalance(7, 0), [new PurchaseLine(product, true, 2)], Settings).Outcome!;

        var first = LoyaltyAction.Record(account, LoyaltyActionType.Purchase, actor, key, "hash", outcome, Now, session);
        var second = LoyaltyAction.Record(account, LoyaltyActionType.Purchase, actor, Guid.NewGuid(), "hash", outcome, Now, session);

        Assert.Equal(1, first.Sequence);
        Assert.Equal(2, second.Sequence);
        Assert.Equal(2, account.ActionCount);
        Assert.Equal(account.Id, first.AccountId);
        Assert.Equal(key, first.IdempotencyKey);
        Assert.Equal(new LoyaltyBalance(1, 1), first.BalanceAfter);
        Assert.Equal(3, first.Entries.Count);
        Assert.All(first.Entries, entry =>
        {
            Assert.Equal(first.Id, entry.ActionId);
            Assert.Equal(account.Id, entry.AccountId);
            Assert.Equal(Now, entry.CreatedAt);
        });
        Assert.Equal(first.Entries.Count, first.Entries.Select(e => e.Id).Distinct().Count());
        Assert.Equal([1, 2, 3], first.Entries.Select(e => e.Line));
        Assert.Equal(LedgerEntryKind.RewardEarned, first.Entries[^1].Kind);
    }

    [Fact]
    public void Record_PurchaseWithoutScanSession_IsRejected()
    {
        var account = CustomerAccount.Open(Guid.NewGuid(), Now);
        var outcome = LoyaltyRules.Purchase(LoyaltyBalance.Zero, [new PurchaseLine(Guid.NewGuid(), true, 1)], Settings).Outcome!;

        Assert.Throws<ArgumentException>(() =>
            LoyaltyAction.Record(account, LoyaltyActionType.Purchase, Guid.NewGuid(), Guid.NewGuid(), "hash", outcome, Now));
        Assert.Equal(0, account.ActionCount);
    }

    [Fact]
    public void Record_ReversalNeedsTargetAndReason()
    {
        var account = CustomerAccount.Open(Guid.NewGuid(), Now);
        var outcome = LoyaltyRules.Purchase(LoyaltyBalance.Zero, [new PurchaseLine(Guid.NewGuid(), true, 1)], Settings).Outcome!;

        Assert.Throws<ArgumentNullException>(() => LoyaltyAction.Record(
            account, LoyaltyActionType.Reversal, Guid.NewGuid(), Guid.NewGuid(), "hash", outcome, Now, reason: "причина"));
        Assert.Throws<ArgumentException>(() => LoyaltyAction.Record(
            account, LoyaltyActionType.Reversal, Guid.NewGuid(), Guid.NewGuid(), "hash", outcome, Now,
            reversesActionId: Guid.NewGuid(), reason: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoyaltyAction.Record(
            account, LoyaltyActionType.Reversal, Guid.NewGuid(), Guid.NewGuid(), "hash", outcome, Now,
            reversesActionId: Guid.NewGuid(), reason: new string('а', LoyaltyAction.ReasonMaxLength + 1)));
        Assert.Equal(0, account.ActionCount);
    }

    [Fact]
    public void Record_OutcomeWithoutEntries_IsRejected()
    {
        var account = CustomerAccount.Open(Guid.NewGuid(), Now);
        var empty = new RuleOutcome([], LoyaltyBalance.Zero, LoyaltyBalance.Zero);

        Assert.Throws<ArgumentException>(() => LoyaltyAction.Record(
            account, LoyaltyActionType.Purchase, Guid.NewGuid(), Guid.NewGuid(), "hash", empty, Now, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0, 4, 5)]
    [InlineData(8, 0, 5)]
    [InlineData(8, 4, -1)]
    public void Settings_RejectValuesThatBreakTheRules(int threshold, int maxPerTransaction, int undoMinutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoyaltySettings.Create(threshold, maxPerTransaction, undoMinutes));
    }

    [Fact]
    public void Settings_DefaultsMatchTheProgramme()
    {
        var settings = LoyaltySettings.Default();

        Assert.Equal(8, settings.StampThreshold);
        Assert.Equal(4, settings.MaxStampsPerTransaction);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.UndoWindow);
        Assert.Null(settings.StampValidityDays);
        Assert.Throws<ArgumentOutOfRangeException>(() => LoyaltySettings.Create(8, 4, 5, stampValidityDays: 0));
    }

    [Fact]
    public void Product_ValidatesItsData()
    {
        var product = Product.Create(Guid.NewGuid(), " Cappuccino ", 240, 2.60m, earnsStamp: true, sortOrder: 3);

        Assert.Equal("Cappuccino", product.Name);
        Assert.True(product.IsActive);
        Assert.Throws<ArgumentException>(() => Product.Create(Guid.NewGuid(), " ", 240, 2.60m, true, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Product.Create(Guid.NewGuid(), "Latte", 0, 2.60m, true, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Product.Create(Guid.NewGuid(), "Latte", 240, -0.01m, true, 1));
    }
}
