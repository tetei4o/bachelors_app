using StoryCups.Domain.Identity;

namespace StoryCups.UnitTests.Domain;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private static RefreshToken NewToken() =>
        RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", Now, Lifetime);

    [Fact]
    public void Issue_SetsExpiryFromLifetime_AndStartsActive()
    {
        var token = NewToken();

        Assert.Equal(Now + Lifetime, token.ExpiresAt);
        Assert.True(token.IsActive(Now));
        Assert.Null(token.RevokedAt);
        Assert.NotEqual(Guid.Empty, token.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Issue_RejectsNonPositiveLifetime(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", Now, TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Issue_RejectsEmptyHash()
    {
        Assert.Throws<ArgumentException>(() => RefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), " ", Now, Lifetime));
    }

    [Fact]
    public void IsExpired_IsTrueExactlyAtExpiry()
    {
        var token = NewToken();

        Assert.False(token.IsExpired(token.ExpiresAt.AddTicks(-1)));
        Assert.True(token.IsExpired(token.ExpiresAt));
    }

    [Fact]
    public void MarkReplaced_RevokesWithRotatedReason_AndLinksReplacement()
    {
        var token = NewToken();
        var replacementId = Guid.NewGuid();

        token.MarkReplaced(replacementId, Now.AddMinutes(1));

        Assert.True(token.IsRevoked);
        Assert.Equal(RefreshTokenRevocationReason.Rotated, token.RevocationReason);
        Assert.Equal(replacementId, token.ReplacedByTokenId);
    }

    [Fact]
    public void MarkReplaced_OnRevokedToken_Throws()
    {
        var token = NewToken();
        token.Revoke(Now, RefreshTokenRevocationReason.Logout);

        Assert.Throws<InvalidOperationException>(() => token.MarkReplaced(Guid.NewGuid(), Now));
    }

    [Fact]
    public void MarkReplaced_OnExpiredToken_Throws()
    {
        var token = NewToken();

        Assert.Throws<InvalidOperationException>(() => token.MarkReplaced(Guid.NewGuid(), token.ExpiresAt));
    }

    [Fact]
    public void Revoke_IsIdempotent_AndKeepsFirstReason()
    {
        var token = NewToken();

        token.Revoke(Now, RefreshTokenRevocationReason.Logout);
        token.Revoke(Now.AddMinutes(5), RefreshTokenRevocationReason.ReuseDetected);

        Assert.Equal(RefreshTokenRevocationReason.Logout, token.RevocationReason);
        Assert.Equal(Now, token.RevokedAt);
    }
}
