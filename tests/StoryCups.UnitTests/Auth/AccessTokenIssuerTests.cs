using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StoryCups.Infrastructure.Auth;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.UnitTests.Auth;

public sealed class AccessTokenIssuerTests
{
    private readonly JwtOptions options = new()
    {
        SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
    };

    private readonly ApplicationUser user = new()
    {
        Id = Guid.NewGuid(),
        Email = "client@example.com",
        DisplayName = "Тест Клиент",
    };

    [Fact]
    public async Task Issue_CreatesValidTokenWithExpectedClaims()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var issuer = new AccessTokenIssuer(Options.Create(options), clock);

        var (token, expiresAt) = issuer.Issue(user, ["Customer"]);

        Assert.Equal(clock.GetUtcNow() + options.AccessTokenLifetime, expiresAt);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, options.CreateValidationParameters());
        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(user.Id.ToString(), result.ClaimsIdentity.FindFirst(AccessTokenClaims.Subject)?.Value);
        Assert.Equal("Тест Клиент", result.ClaimsIdentity.Name);
        Assert.True(result.ClaimsIdentity.HasClaim(AccessTokenClaims.Role, "Customer"));
    }

    [Fact]
    public async Task Issue_TokenFromThePast_IsRejectedAsExpired()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow.AddMinutes(-20));
        var issuer = new AccessTokenIssuer(Options.Create(options), clock);

        var (token, _) = issuer.Issue(user, []);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, options.CreateValidationParameters());
        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(result.Exception);
    }

    [Fact]
    public async Task Validation_RejectsTokenSignedWithAnotherKey()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var other = new JwtOptions { SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        var (token, _) = new AccessTokenIssuer(Options.Create(other), clock).Issue(user, []);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, options.CreateValidationParameters());

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("c2hvcnQ=")]
    public void CreateSigningKey_RejectsInvalidOrShortKey(string key)
    {
        var invalid = new JwtOptions { SigningKey = key };

        Assert.Throws<InvalidOperationException>(invalid.CreateSigningKey);
    }
}
