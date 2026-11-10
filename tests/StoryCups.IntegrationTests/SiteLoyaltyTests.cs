using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using StoryCups.Contracts.Common;
using StoryCups.Contracts.Loyalty;
using StoryCups.Domain.Identity;
using StoryCups.Infrastructure.Seeding;
using StoryCups.IntegrationTests.Infrastructure;
using StoryCups.Web.Services;

namespace StoryCups.IntegrationTests;

/// <summary>
/// Услугите зад общите екрани в сайта (ADR-003): същите use case-ове като API-то, но извикани без HTTP
/// от интерактивната връзка. Ролята се проверява в услугата, не само в страницата.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class SiteLoyaltyTests(StoryCupsAppFactory app)
{
    private static readonly Guid Cappuccino = MenuSeed.Drinks[2].Id;

    private readonly HttpClient client = app.CreateClient(new() { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/card")]
    [InlineData("/barista")]
    public async Task Pages_Anonymous_RedirectToLogin(string path)
    {
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Home_ShowsMenuAndThresholdFromTheDatabase()
    {
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        Assert.All(MenuSeed.Drinks, drink => Assert.Contains(drink.Name, html));
        Assert.Contains("8 печата =", html);
        Assert.Contains("След 8 печата", html);
    }

    [Fact]
    public async Task SiteServices_RunTheWholeVisit_WithoutHttp()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var card = CardService(customer, AppRoles.Customer);
        var counter = BaristaService(barista, AppRoles.Barista);

        var token = await card.IssueQrTokenAsync();
        var session = await counter.ScanAsync(token.Value!.Token);
        var key = Guid.NewGuid();
        var purchase = await counter.SubmitActionAsync(session.Value!.Id, ApiClient.Purchase((Cappuccino, 2)), key);
        var replay = await counter.SubmitActionAsync(session.Value.Id, ApiClient.Purchase((Cappuccino, 2)), key);
        var products = await counter.GetProductsAsync();

        Assert.True(purchase.IsSuccess, purchase.ErrorMessage);
        Assert.Equal(purchase.Value!.ActionId, replay.Value!.ActionId);
        Assert.Equal(12, products.Value!.Count);
        Assert.Equal(new LoyaltyStatusDto(2, 0, 8), (await card.GetStatusAsync()).Value);
        Assert.Single((await card.GetHistoryAsync()).Value!.Items);

        var reversal = await counter.ReverseAsync(purchase.Value.ActionId, "грешен брой", Guid.NewGuid());
        Assert.True(reversal.IsSuccess, reversal.ErrorMessage);
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), (await card.GetStatusAsync()).Value);
    }

    [Fact]
    public async Task SiteServices_CheckTheRoleThemselves()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var token = (await CardService(customer, AppRoles.Customer).IssueQrTokenAsync()).Value!.Token;

        // Клиент, стигнал до екрана на баристата, и служител, стигнал до картата: услугата отказва и на двамата.
        var customerAtCounter = BaristaService(customer, AppRoles.Customer);
        var scan = await customerAtCounter.ScanAsync(token);
        var action = await customerAtCounter.SubmitActionAsync(Guid.NewGuid(), ApiClient.Purchase((Cappuccino, 1)), Guid.NewGuid());
        var staffCard = await CardService(barista, AppRoles.Barista).IssueQrTokenAsync();
        var anonymous = await new ServerBaristaService(
            new FixedAuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())),
            app.Services.GetRequiredService<IServiceScopeFactory>(),
            app.Services.GetRequiredService<SiteRateLimiter>()).GetProductsAsync();

        Assert.Equal(ClientErrorCodes.Forbidden, scan.ErrorCode);
        Assert.Equal(ClientErrorCodes.Forbidden, action.ErrorCode);
        Assert.Equal(ClientErrorCodes.Forbidden, staffCard.ErrorCode);
        Assert.Equal(ClientErrorCodes.Unauthorized, anonymous.ErrorCode);

        // Отказаният опит не е използвал кода.
        Assert.True((await BaristaService(barista, AppRoles.Barista).ScanAsync(token)).IsSuccess);
    }

    [Fact]
    public async Task SiteServices_ReturnTheSameErrorCodesAsTheApi()
    {
        var barista = await app.NewStaffAsync(client);
        var counter = BaristaService(barista, AppRoles.Barista);

        var invalid = await counter.ScanAsync("това не е код");
        var noSession = await counter.SubmitActionAsync(Guid.NewGuid(), ApiClient.Purchase((Cappuccino, 1)), Guid.NewGuid());

        Assert.Equal(LoyaltyErrorCodes.QrInvalid, invalid.ErrorCode);
        Assert.Equal("Невалиден код.", invalid.ErrorMessage);
        Assert.Equal(LoyaltyErrorCodes.ScanSessionNotFound, noSession.ErrorCode);
        Assert.False(invalid.IsNetworkError);
    }

    private ServerCustomerCardService CardService(TestUser user, string role) => new(
        new FixedAuthenticationState(Principal(user, role)),
        app.Services.GetRequiredService<IServiceScopeFactory>(),
        app.Services.GetRequiredService<SiteRateLimiter>());

    private ServerBaristaService BaristaService(TestUser user, string role) => new(
        new FixedAuthenticationState(Principal(user, role)),
        app.Services.GetRequiredService<IServiceScopeFactory>(),
        app.Services.GetRequiredService<SiteRateLimiter>());

    /// <summary>Потребителят, както го дава бисквитката на сайта: Id и роля в стандартните claims.</summary>
    private static ClaimsPrincipal Principal(TestUser user, string role) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, role)],
        authenticationType: "test"));

    private sealed class FixedAuthenticationState(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }
}
