using StoryCups.Domain.Qr;

namespace StoryCups.UnitTests.Qr;

public sealed class ScanSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Open_BindsBaristaAndAccount_AndExpiresAfterLifetime()
    {
        var barista = Guid.NewGuid();
        var account = Guid.NewGuid();

        var session = ScanSession.Open(barista, account, Now, TimeSpan.FromMinutes(2));

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal(account, session.AccountId);
        Assert.True(session.BelongsTo(barista));
        Assert.False(session.BelongsTo(Guid.NewGuid()));
        Assert.False(session.IsExpired(Now.AddSeconds(119)));
        Assert.True(session.IsExpired(Now.AddSeconds(120)));
    }

    [Fact]
    public void Open_RejectsNonPositiveLifetime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScanSession.Open(Guid.NewGuid(), Guid.NewGuid(), Now, TimeSpan.Zero));
    }

    [Fact]
    public void UsedQrNonce_CopiesNonceAndExpiryFromThePayload()
    {
        var nonce = new byte[QrTokenFormat.NonceLength];
        nonce[0] = 42;
        var payload = new QrTokenPayload(1, Guid.NewGuid(), Now, Now.AddSeconds(60), nonce);

        var used = UsedQrNonce.From(payload);
        nonce[0] = 0;

        Assert.Equal(42, used.Nonce[0]);
        Assert.Equal(payload.ExpiresAt, used.ExpiresAt);
        Assert.Throws<ArgumentException>(() => UsedQrNonce.From(payload with { Nonce = new byte[8] }));
    }
}
