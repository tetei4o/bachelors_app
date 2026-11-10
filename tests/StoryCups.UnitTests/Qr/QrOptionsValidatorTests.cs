using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using StoryCups.Infrastructure.Auth;
using StoryCups.Infrastructure.Qr;

namespace StoryCups.UnitTests.Qr;

public sealed class QrOptionsValidatorTests
{
    private static readonly string JwtKey = NewKey();

    private static string NewKey(int bytes = 32) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));

    private static QrOptions ValidOptions() => new()
    {
        ActiveKeyId = 1,
        Keys = [new QrKeyOptions { Id = 1, Key = NewKey() }],
    };

    private static ValidateOptionsResult Validate(QrOptions options) =>
        new QrOptionsValidator(Options.Create(new JwtOptions { SigningKey = JwtKey })).Validate(null, options);

    [Fact]
    public void Defaults_WithOneKey_AreValid()
    {
        var options = ValidOptions();

        Assert.True(Validate(options).Succeeded);
        Assert.Equal(TimeSpan.FromSeconds(60), options.TokenLifetime);
        Assert.Equal(TimeSpan.FromSeconds(20), options.RefreshBeforeExpiry);
        Assert.Equal(TimeSpan.FromMinutes(2), options.ScanSessionLifetime);
    }

    [Fact]
    public void MissingKey_FailsWithHintForUserSecrets()
    {
        var options = ValidOptions();
        options.Keys[0].Key = string.Empty;

        var result = Validate(options);

        Assert.True(result.Failed);
        Assert.Contains("Qr:Keys:0:Key", result.FailureMessage);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("c2hvcnQ=")]
    public void InvalidOrShortKey_Fails(string key)
    {
        var options = ValidOptions();
        options.Keys[0].Key = key;

        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void SameKeyAsJwt_Fails()
    {
        var options = ValidOptions();
        options.Keys[0].Key = JwtKey;

        var result = Validate(options);

        Assert.True(result.Failed);
        Assert.Contains("Jwt:SigningKey", result.FailureMessage);
    }

    [Fact]
    public void ActiveKeyNotAmongKeys_OrDuplicateIds_Fail()
    {
        var missingActive = ValidOptions();
        missingActive.ActiveKeyId = 2;

        var duplicates = ValidOptions();
        duplicates.Keys.Add(new QrKeyOptions { Id = 1, Key = NewKey() });

        Assert.True(Validate(missingActive).Failed);
        Assert.True(Validate(duplicates).Failed);
    }

    [Theory]
    [InlineData(29, 20, false)]
    [InlineData(30, 20, true)]
    [InlineData(120, 20, true)]
    [InlineData(121, 20, false)]
    [InlineData(60, 60, false)]
    [InlineData(60, 0, false)]
    public void TokenLifetime_MustBeBetween30And120Seconds_AndLongerThanRefreshMargin(int lifetime, int refreshBefore, bool valid)
    {
        var options = ValidOptions();
        options.TokenLifetime = TimeSpan.FromSeconds(lifetime);
        options.RefreshBeforeExpiry = TimeSpan.FromSeconds(refreshBefore);

        Assert.Equal(valid, Validate(options).Succeeded);
    }

    [Fact]
    public void CreateCodec_UsesTheActiveKey()
    {
        var options = ValidOptions();
        options.Keys.Add(new QrKeyOptions { Id = 2, Key = NewKey() });
        options.ActiveKeyId = 2;
        var now = DateTimeOffset.UtcNow;

        var codec = options.CreateCodec();
        var issued = codec.Issue(Guid.NewGuid(), now, options.TokenLifetime);

        Assert.Equal(2, issued.Payload.KeyId);
        Assert.True(codec.Validate(issued.Token, now).IsValid);
    }
}
