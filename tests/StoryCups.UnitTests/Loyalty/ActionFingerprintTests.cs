using StoryCups.Domain.Loyalty;

namespace StoryCups.UnitTests.Loyalty;

public sealed class ActionFingerprintTests
{
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly Guid Cappuccino = Guid.NewGuid();
    private static readonly Guid Latte = Guid.NewGuid();

    [Fact]
    public void ForPurchase_IsSha256Hex()
    {
        var hash = ActionFingerprint.ForPurchase(Session, [(Cappuccino, 1)]);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ForPurchase_IgnoresLineOrderAndSplitLines()
    {
        var a = ActionFingerprint.ForPurchase(Session, [(Cappuccino, 2), (Latte, 1)]);
        var b = ActionFingerprint.ForPurchase(Session, [(Latte, 1), (Cappuccino, 1), (Cappuccino, 1)]);

        Assert.Equal(a, b);
    }

    [Fact]
    public void ForPurchase_ChangesWithSessionProductOrQuantity()
    {
        var original = ActionFingerprint.ForPurchase(Session, [(Cappuccino, 2)]);

        Assert.NotEqual(original, ActionFingerprint.ForPurchase(Guid.NewGuid(), [(Cappuccino, 2)]));
        Assert.NotEqual(original, ActionFingerprint.ForPurchase(Session, [(Latte, 2)]));
        Assert.NotEqual(original, ActionFingerprint.ForPurchase(Session, [(Cappuccino, 3)]));
    }

    [Fact]
    public void DifferentActionTypes_NeverShareAFingerprint()
    {
        var purchase = ActionFingerprint.ForPurchase(Session, [(Cappuccino, 1)]);
        var redemption = ActionFingerprint.ForRedemption(Session, Cappuccino);
        var reversal = ActionFingerprint.ForReversal(Session, "причина");

        Assert.NotEqual(purchase, redemption);
        Assert.NotEqual(redemption, reversal);
        Assert.NotEqual(reversal, ActionFingerprint.ForReversal(Session, "друга причина"));
        Assert.Equal(redemption, ActionFingerprint.ForRedemption(Session, Cappuccino));
    }
}
