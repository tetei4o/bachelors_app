using System.Net;
using StoryCups.IntegrationTests.Infrastructure;
using StoryCups.Web.Content;

namespace StoryCups.IntegrationTests;

/// <summary>Публичната част е достъпна без профил, а личните страници не са (ADR-013).</summary>
[Collection(AppCollection.Name)]
public sealed class PublicSiteTests(StoryCupsAppFactory app)
{
    // Маркерът, който Blazor оставя за интерактивен сървърен компонент (SignalR връзка).
    private const string InteractiveServerMarker = "\"type\":\"server\"";

    private readonly HttpClient client = app.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Home_Anonymous_ShowsMenuAndLoyaltyWithoutInteractiveCircuit()
    {
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Cappuccino", html);
        Assert.Contains("2,60 €", html);
        Assert.Contains("Caffè Leccese", html);
        Assert.Contains(CafeInfo.Address, html);
        Assert.Contains("id=\"loyalty\"", html);
        Assert.Contains("href=\"Account/Register\"", html);
        Assert.DoesNotContain(InteractiveServerMarker, html);
    }

    [Fact]
    public async Task Home_Anonymous_FooterLinksToInstagramAndFacebookWithScreenReaderText()
    {
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        // Иконите са скрити от екранния четец, затова всяка връзка носи свой текст.
        Assert.Contains($"href=\"{CafeInfo.InstagramUrl}\"", html);
        Assert.Contains($"href=\"{CafeInfo.FacebookUrl}\"", html);
        Assert.Contains("Instagram (отваря се в нов раздел)", html);
        Assert.Contains("Facebook (отваря се в нов раздел)", html);
    }

    [Theory]
    [InlineData("logo-secondary-white.svg")]
    [InlineData("logo-primary-white.svg")]
    public async Task Home_UsesOfficialLogoFilesThatAreServed(string file)
    {
        // Официалните файлове от собственика (ADR-014): страницата ги ползва и сървърът ги връща.
        var html = await client.GetStringAsync("/");
        Assert.Contains($"_content/StoryCups.Shared/brand/{file}", html);

        var asset = await client.GetAsync($"/_content/StoryCups.Shared/brand/{file}");
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal("image/svg+xml", asset.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Privacy_Anonymous_IsStaticPage()
    {
        var response = await client.GetAsync("/privacy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(InteractiveServerMarker, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Profile_Anonymous_RedirectsToLogin()
    {
        var response = await client.GetAsync("/profile");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Account/Login", response.Headers.Location!.ToString());
    }
}
