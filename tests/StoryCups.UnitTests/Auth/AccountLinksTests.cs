using Microsoft.Extensions.Options;
using StoryCups.Infrastructure.Email;

namespace StoryCups.UnitTests.Auth;

public sealed class AccountLinksTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://example.com/")]
    public void Links_AreBuiltFromConfiguredBaseUrl(string baseUrl)
    {
        var links = new AccountLinks(Options.Create(new AppUrlOptions { PublicBaseUrl = baseUrl }));
        var userId = Guid.Parse("0195f0a0-0000-7000-8000-000000000001");

        Assert.Equal("https://example.com/Account/Login", links.Login());
        Assert.Equal(
            "https://example.com/Account/ConfirmEmail?userId=0195f0a0-0000-7000-8000-000000000001&code=abc_-",
            links.ConfirmEmail(userId, "abc_-"));
        Assert.Equal("https://example.com/Account/ResetPassword?code=xyz", links.ResetPassword("xyz"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/relative")]
    [InlineData("ftp://example.com")]
    public void GetBaseUri_RejectsNonHttpUrls(string baseUrl)
    {
        var options = new AppUrlOptions { PublicBaseUrl = baseUrl };

        Assert.Throws<InvalidOperationException>(options.GetBaseUri);
    }
}
