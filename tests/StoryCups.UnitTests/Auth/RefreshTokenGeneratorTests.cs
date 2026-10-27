using StoryCups.Infrastructure.Auth;

namespace StoryCups.UnitTests.Auth;

public sealed class RefreshTokenGeneratorTests
{
    [Fact]
    public void Generate_Returns256BitBase64UrlToken()
    {
        var token = RefreshTokenGenerator.Generate();

        // 32 байта в base64url без padding = 43 символа.
        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    [Fact]
    public void Generate_ReturnsDifferentTokens()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => RefreshTokenGenerator.Generate()).ToHashSet();

        Assert.Equal(100, tokens.Count);
    }

    [Fact]
    public void Hash_IsDeterministicLowercaseSha256Hex()
    {
        var first = RefreshTokenGenerator.Hash("abc");

        Assert.Equal(first, RefreshTokenGenerator.Hash("abc"));
        // SHA-256("abc"), FIPS 180-2, пример B.1.
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", first);
    }
}
