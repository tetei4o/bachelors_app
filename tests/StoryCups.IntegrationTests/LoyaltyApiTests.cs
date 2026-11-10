using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StoryCups.Contracts;
using StoryCups.Contracts.Loyalty;
using StoryCups.Domain.Identity;
using StoryCups.Domain.Loyalty;
using StoryCups.Domain.Qr;
using StoryCups.Infrastructure.Persistence;
using StoryCups.Infrastructure.Seeding;
using StoryCups.IntegrationTests.Infrastructure;

namespace StoryCups.IntegrationTests;

/// <summary>
/// Печати, награди и отмяна през HTTP срещу истински PostgreSQL (ADR-008, ADR-015): правилата, идемпотентността,
/// едновременните действия и неизменността на ledger-а.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class LoyaltyApiTests(StoryCupsAppFactory app)
{
    private static readonly Guid Cappuccino = MenuSeed.Drinks[2].Id;
    private static readonly Guid Latte = MenuSeed.Drinks[5].Id;

    private readonly HttpClient client = app.CreateClient();

    [Fact]
    public async Task Products_Staff_GetsTheMenuInBoardOrder()
    {
        var barista = await app.NewStaffAsync(client);
        var customer = await app.NewCustomerAsync(client);

        var response = await client.SendAsync(HttpMethod.Get, ApiRoutes.Loyalty.Products, barista.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = (await response.Content.ReadFromJsonAsync<List<ProductDto>>())!;
        Assert.Equal(12, products.Count);
        Assert.Equal("Single Espresso", products[0].Name);
        Assert.Equal("Caffè Leccese", products[^1].Name);
        Assert.Contains(products, p => p is { Name: "Cappuccino", VolumeMl: 240, PriceEur: 2.60m, EarnsStamp: true });

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(HttpMethod.Get, ApiRoutes.Loyalty.Products, customer.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(HttpMethod.Get, ApiRoutes.Loyalty.Products, null)).StatusCode);
    }

    [Fact]
    public async Task Purchase_AddsStamps_AndTheCustomerSeesThem()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), session.Loyalty);

        var result = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, quantity: 2);

        Assert.Equal(LoyaltyActionTypes.Purchase, result.Type);
        Assert.Equal(2, result.StampsAdded);
        Assert.Equal(0, result.RewardsEarned);
        Assert.Equal(new LoyaltyStatusDto(2, 0, 8), result.Loyalty);
        Assert.Equal(result.CreatedAt.AddMinutes(5), result.UndoUntil);
        Assert.Null(result.ReversedActionId);
        Assert.Equal(new LoyaltyStatusDto(2, 0, 8), await client.StatusAsync(customer));

        // Следващото сканиране показва на баристата новия баланс.
        Assert.Equal(new LoyaltyStatusDto(2, 0, 8), (await client.ScanOkAsync(barista, customer)).Loyalty);
    }

    [Fact]
    public async Task Purchase_SeveralActionsInOneSession_AreAllowed()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);

        await client.PurchaseOkAsync(barista, session.Id, Cappuccino);
        var second = await client.PurchaseOkAsync(barista, session.Id, Latte);

        Assert.Equal(new LoyaltyStatusDto(2, 0, 8), second.Loyalty);
    }

    [Fact]
    public async Task Purchase_ReachingTheThreshold_EarnsAReward()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 4);
        await client.PurchaseOkAsync(barista, session.Id, Latte, 2);

        // Примерът от раздел 2.4: 6 печата и 3 напитки дават 1 печат и 1 награда.
        var result = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 3);

        Assert.Equal(3, result.StampsAdded);
        Assert.Equal(1, result.RewardsEarned);
        Assert.Equal(new LoyaltyStatusDto(1, 1, 8), result.Loyalty);
        Assert.Equal(new LoyaltyStatusDto(1, 1, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Purchase_IsCappedAtFourStampsPerPurchase()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);

        var result = await (await client.SubmitActionAsync(
            barista, session.Id, ApiClient.Purchase((Cappuccino, 3), (Latte, 3)))).ActionOkAsync();

        Assert.Equal(4, result.StampsAdded);
        Assert.Equal(new LoyaltyStatusDto(4, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Redeem_UsesOneReward_AndWithoutRewardIsRefused()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 4);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 4);

        var redeemed = await client.RedeemOkAsync(barista, session.Id, Latte);
        var again = await client.SubmitActionAsync(barista, session.Id, ApiClient.Redeem(Latte));

        Assert.Equal(LoyaltyActionTypes.RedeemReward, redeemed.Type);
        Assert.Equal(1, redeemed.RewardsRedeemed);
        Assert.Equal(0, redeemed.StampsAdded);
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), redeemed.Loyalty);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.NoRewardAvailable, await again.ErrorCodeAsync());
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), await client.StatusAsync(customer));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("не-е-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Action_WithoutValidIdempotencyKey_Returns400(string? key)
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);

        var response = await client.SendAsync(
            HttpMethod.Post, ApiRoutes.Loyalty.ActionsFor(session.Id), barista.AccessToken, ApiClient.Purchase((Cappuccino, 1)), key);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.IdempotencyKeyRequired, await response.ErrorCodeAsync());
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Action_RepeatedWithTheSameKey_IsNotAppliedTwice()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        var key = Guid.NewGuid();

        var first = await client.PurchaseAsync(barista, session.Id, Cappuccino, 2, key);
        var second = await client.PurchaseAsync(barista, session.Id, Cappuccino, 2, key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.False(first.Headers.Contains(ApiHeaders.IdempotentReplayed));
        Assert.Equal("true", second.Headers.GetValues(ApiHeaders.IdempotentReplayed).Single());

        // Повторената заявка получава същия отговор, а балансът е начислен веднъж.
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(new LoyaltyStatusDto(2, 0, 8), await client.StatusAsync(customer));
        Assert.Single((await client.HistoryAsync(customer)).Items);
    }

    [Fact]
    public async Task Action_SameKeyWithDifferentContent_Returns422()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        var key = Guid.NewGuid();
        await (await client.PurchaseAsync(barista, session.Id, Cappuccino, 1, key)).ActionOkAsync();

        var response = await client.PurchaseAsync(barista, session.Id, Cappuccino, 3, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.IdempotencyKeyReused, await response.ErrorCodeAsync());
        Assert.Equal(new LoyaltyStatusDto(1, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Action_SameKeyFromAnotherBarista_IsASeparateAction()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var other = await app.NewStaffAsync(client);
        var key = Guid.NewGuid();
        var first = await client.ScanOkAsync(barista, customer);
        var second = await client.ScanOkAsync(other, customer);

        await (await client.PurchaseAsync(barista, first.Id, Cappuccino, 1, key)).ActionOkAsync();
        await (await client.PurchaseAsync(other, second.Id, Cappuccino, 1, key)).ActionOkAsync();

        // Ключът важи в рамките на един служител: чуждият ключ не може да отгатне или блокира действие.
        Assert.Equal(new LoyaltyStatusDto(2, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Action_SameKeyConcurrently_IsAppliedOnce()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        var key = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => client.PurchaseAsync(barista, session.Id, Cappuccino, 1, key)));

        var results = new List<LoyaltyActionResultDto>();
        foreach (var response in responses)
        {
            results.Add(await response.ActionOkAsync());
        }

        Assert.Single(results.Select(r => r.ActionId).Distinct());
        Assert.Single(responses, r => !r.Headers.Contains(ApiHeaders.IdempotentReplayed));
        Assert.Equal(new LoyaltyStatusDto(1, 0, 8), await client.StatusAsync(customer));
    }

    /// <summary>
    /// Четири едновременни действия по една сметка, които заедно минават прага. Без optimistic concurrency
    /// всички биха прочели баланс 6 и биха записали 7 без награда; тук нищо не се губи.
    /// </summary>
    [Fact]
    public async Task Actions_ConcurrentOnOneAccount_LoseNothing()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 4);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 2);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => client.PurchaseAsync(barista, session.Id, Latte)));

        var results = new List<LoyaltyActionResultDto>();
        foreach (var response in responses)
        {
            results.Add(await response.ActionOkAsync());
        }

        // 6 + 4 = 10 печата: една награда и 2 печата. Точно едно от действията е донесло наградата.
        Assert.Equal(new LoyaltyStatusDto(2, 1, 8), await client.StatusAsync(customer));
        Assert.Equal(1, results.Sum(r => r.RewardsEarned));
        Assert.Equal(
            [(7, 0), (0, 1), (1, 1), (2, 1)],
            results.Select(r => (r.Loyalty.Stamps, r.Loyalty.Rewards)).OrderBy(b => b.Rewards).ThenBy(b => b.Stamps == 7 ? -1 : b.Stamps));

        var sequences = (await client.HistoryAsync(customer)).Items.Select(item => item.Sequence).ToList();
        Assert.Equal([6, 5, 4, 3, 2, 1], sequences);
        await AssertBalanceMatchesLedgerAsync(customer);
    }

    [Fact]
    public async Task Action_InUnknownOrForeignSession_Returns404()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var other = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);

        var unknown = await client.PurchaseAsync(barista, Guid.NewGuid(), Cappuccino);
        var foreign = await client.PurchaseAsync(other, session.Id, Cappuccino);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.ScanSessionNotFound, await unknown.ErrorCodeAsync());

        // Сесията важи само за баристата, който е сканирал кода.
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Action_InExpiredSession_IsGone()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var live = await client.ScanOkAsync(barista, customer);
        var expired = await AddSessionAsync(barista, customer, openedAt: DateTimeOffset.UtcNow.AddMinutes(-3));

        var response = await client.PurchaseAsync(barista, expired.Id, Cappuccino);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.ScanSessionExpired, await response.ErrorCodeAsync());
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), live.Loyalty);
    }

    [Fact]
    public async Task Action_WithUnknownProductOrBadBody_IsRefused()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);

        var unknownProduct = await client.PurchaseAsync(barista, session.Id, Guid.NewGuid());
        var noItems = await client.SubmitActionAsync(barista, session.Id, new LoyaltyActionRequest { Type = LoyaltyActionTypes.Purchase });
        var badType = await client.SubmitActionAsync(barista, session.Id, new LoyaltyActionRequest { Type = "refund" });
        var tooMany = await client.PurchaseAsync(barista, session.Id, Cappuccino, quantity: 21);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownProduct.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.UnknownProduct, await unknownProduct.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, noItems.StatusCode);
        Assert.Contains("\"Items\"", await noItems.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Action_ByCustomerOrAnonymous_IsRefused()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);

        // Клиентът не може сам да си начисли печат, дори да знае идентификатора на сесията.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PurchaseAsync(customer, session.Id, Cappuccino)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SubmitActionAsync(null, session.Id, ApiClient.Purchase((Cappuccino, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.ReverseAsync(customer, Guid.NewGuid())).StatusCode);
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Reversal_UndoesThePurchaseWithOppositeEntries()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        var purchase = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 3);

        var reversal = await (await client.ReverseAsync(barista, purchase.ActionId)).ActionOkAsync();

        Assert.Equal(LoyaltyActionTypes.Reversal, reversal.Type);
        Assert.Equal(purchase.ActionId, reversal.ReversedActionId);
        Assert.Null(reversal.UndoUntil);
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), reversal.Loyalty);
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), await client.StatusAsync(customer));

        // Първоначалните записи остават; отмяната добавя по един обратен запис за всеки от тях.
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var original = await db.LedgerEntries.Where(e => e.ActionId == purchase.ActionId).ToListAsync();
        var opposite = await db.LedgerEntries.Where(e => e.ActionId == reversal.ActionId).ToListAsync();
        Assert.Equal(3, original.Count);
        Assert.All(original, e => Assert.Equal((LedgerEntryKind.Purchase, 1), (e.Kind, e.StampDelta)));
        Assert.Equal(3, opposite.Count);
        Assert.All(opposite, e => Assert.Equal((LedgerEntryKind.Reversal, -1), (e.Kind, e.StampDelta)));
        Assert.Equal(original.Select(e => e.Id).Order(), opposite.Select(e => e.ReversesEntryId!.Value).Order());
        var stored = await db.LoyaltyActions.SingleAsync(a => a.Id == reversal.ActionId);
        Assert.Equal("грешно въведен брой", stored.Reason);
        Assert.Null(stored.ScanSessionId);
    }

    [Fact]
    public async Task Reversal_OfThePurchaseThatEarnedAReward_TakesTheRewardBack()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 4);
        var second = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 4);
        Assert.Equal(new LoyaltyStatusDto(0, 1, 8), second.Loyalty);

        var reversal = await (await client.ReverseAsync(barista, second.ActionId)).ActionOkAsync();

        Assert.Equal(new LoyaltyStatusDto(4, 0, 8), reversal.Loyalty);
        await AssertBalanceMatchesLedgerAsync(customer);
    }

    [Fact]
    public async Task Reversal_OfARedemption_ReturnsTheReward()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 4);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 4);
        var redeemed = await client.RedeemOkAsync(barista, session.Id, Latte);

        var reversal = await (await client.ReverseAsync(barista, redeemed.ActionId)).ActionOkAsync();

        Assert.Equal(new LoyaltyStatusDto(0, 1, 8), reversal.Loyalty);
    }

    [Fact]
    public async Task Reversal_Twice_IsRefused_AndARepeatedRequestIsReplayed()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        var purchase = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 2);
        var key = Guid.NewGuid();
        var first = await (await client.ReverseAsync(barista, purchase.ActionId, idempotencyKey: key)).ActionOkAsync();

        var replay = await client.ReverseAsync(barista, purchase.ActionId, idempotencyKey: key);
        var secondAttempt = await client.ReverseAsync(barista, purchase.ActionId);
        var reversalOfReversal = await client.ReverseAsync(barista, first.ActionId);

        Assert.Equal(first.ActionId, (await replay.ActionOkAsync()).ActionId);
        Assert.Equal("true", replay.Headers.GetValues(ApiHeaders.IdempotentReplayed).Single());
        Assert.Equal(HttpStatusCode.Conflict, secondAttempt.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.AlreadyReversed, await secondAttempt.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, reversalOfReversal.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.NotReversible, await reversalOfReversal.ErrorCodeAsync());
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Reversal_ConcurrentRequests_ReverseOnce()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 3);
        var purchase = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 2);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.ReverseAsync(barista, purchase.ActionId)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(new LoyaltyStatusDto(3, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Reversal_OnlyOfTheLastAction_InReverseOrder()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        var first = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 1);
        var second = await client.PurchaseOkAsync(barista, session.Id, Latte, 2);

        var outOfOrder = await client.ReverseAsync(barista, first.ActionId);
        Assert.Equal(HttpStatusCode.Conflict, outOfOrder.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.NotLastAction, await outOfOrder.ErrorCodeAsync());

        // След отмяната на второто действие първото отново е последното действащо.
        await (await client.ReverseAsync(barista, second.ActionId)).ActionOkAsync();
        var afterFirst = await (await client.ReverseAsync(barista, first.ActionId)).ActionOkAsync();

        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), afterFirst.Loyalty);
    }

    [Fact]
    public async Task Reversal_ByAnotherBaristaIsForbidden_ByAdminIsAllowed()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var other = await app.NewStaffAsync(client);
        var admin = await app.NewStaffAsync(client, AppRoles.Admin);
        var session = await client.ScanOkAsync(barista, customer);
        var purchase = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 2);

        var byOther = await client.ReverseAsync(other, purchase.ActionId);
        var byAdmin = await client.ReverseAsync(admin, purchase.ActionId, "грешен клиент");

        Assert.Equal(HttpStatusCode.Forbidden, byOther.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.ReversalForbidden, await byOther.ErrorCodeAsync());
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), (await byAdmin.ActionOkAsync()).Loyalty);
    }

    [Fact]
    public async Task Reversal_AfterTheUndoWindow_IsRefused()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);

        // Действие отпреди шест минути, записано направо в базата: срокът от пет минути е минал.
        Guid actionId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.ScanSessions.SingleAsync(s => s.Id == session.Id);
            var account = await db.CustomerAccounts.SingleAsync(a => a.Id == stored.AccountId);
            var outcome = LoyaltyRules.Purchase(
                LoyaltyBalance.Zero, [new PurchaseLine(Cappuccino, true, 1)], LoyaltySettings.Default()).Outcome!;
            var action = LoyaltyAction.Record(
                account, LoyaltyActionType.Purchase, barista.Id, Guid.NewGuid(),
                ActionFingerprint.ForPurchase(session.Id, [(Cappuccino, 1)]), outcome,
                DateTimeOffset.UtcNow.AddMinutes(-6), session.Id);
            db.LoyaltyActions.Add(action);
            await db.SaveChangesAsync();
            actionId = action.Id;
        }

        var response = await client.ReverseAsync(barista, actionId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.UndoWindowExpired, await response.ErrorCodeAsync());
        Assert.Equal(new LoyaltyStatusDto(1, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Reversal_UnknownActionOrShortReason_IsRefused()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        var purchase = await client.PurchaseOkAsync(barista, session.Id, Cappuccino);

        var unknown = await client.ReverseAsync(barista, Guid.NewGuid());
        var noReason = await client.ReverseAsync(barista, purchase.ActionId, reason: "а");
        var noKey = await client.SendAsync(
            HttpMethod.Post, ApiRoutes.Loyalty.ReversalFor(purchase.ActionId), barista.AccessToken, new ReversalRequest { Reason = "грешка" });

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.ActionNotFound, await unknown.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Contains("\"Reason\"", await noReason.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.IdempotencyKeyRequired, await noKey.ErrorCodeAsync());
    }

    [Fact]
    public async Task History_ShowsActionsNewestFirst_WithPaging()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        await (await client.SubmitActionAsync(barista, session.Id, ApiClient.Purchase((Cappuccino, 2), (Latte, 1)))).ActionOkAsync();
        var mistake = await client.PurchaseOkAsync(barista, session.Id, Latte);
        await (await client.ReverseAsync(barista, mistake.ActionId)).ActionOkAsync();

        var firstPage = await client.HistoryAsync(customer, "?limit=2");
        var secondPage = await client.HistoryAsync(customer, $"?limit=2&before={firstPage.NextBefore}");

        Assert.Equal([3, 2], firstPage.Items.Select(i => i.Sequence));
        Assert.Equal(2, firstPage.NextBefore);
        Assert.Equal(LoyaltyActionTypes.Reversal, firstPage.Items[0].Type);
        Assert.Null(firstPage.Items[0].Products);
        Assert.Equal((3, 0), (firstPage.Items[0].StampsAfter, firstPage.Items[0].RewardsAfter));
        Assert.True(firstPage.Items[1].IsReversed);
        Assert.Equal("Latte", firstPage.Items[1].Products);

        var purchase = Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextBefore);
        Assert.Equal(LoyaltyActionTypes.Purchase, purchase.Type);
        Assert.Equal("Cappuccino ×2, Latte", purchase.Products);
        Assert.Equal(3, purchase.StampsAdded);
        Assert.False(purchase.IsReversed);
    }

    [Fact]
    public async Task StatusAndHistory_BelongToTheSignedInCustomerOnly()
    {
        var customer = await app.NewCustomerAsync(client);
        var newcomer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 2);

        // Клиент без нито едно сканиране още няма сметка: нулев баланс и празна история.
        Assert.Equal(new LoyaltyStatusDto(0, 0, 8), await client.StatusAsync(newcomer));
        Assert.Empty((await client.HistoryAsync(newcomer)).Items);

        var body = await (await client.SendAsync(HttpMethod.Get, ApiRoutes.Loyalty.MyHistory, customer.AccessToken)).Content.ReadAsStringAsync();
        Assert.DoesNotContain(barista.Id.ToString(), body);
        Assert.DoesNotContain("idempotency", body, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(HttpMethod.Get, ApiRoutes.Loyalty.MyStatus, barista.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(HttpMethod.Get, ApiRoutes.Loyalty.MyHistory, null)).StatusCode);
    }

    /// <summary>
    /// Append-only в базата (ADR-015): дори заявка извън приложението не може да промени или изтрие запис.
    /// </summary>
    [Theory]
    [InlineData("UPDATE \"LedgerEntries\" SET \"StampDelta\" = 100")]
    [InlineData("DELETE FROM \"LedgerEntries\"")]
    [InlineData("TRUNCATE \"LedgerEntries\"")]
    [InlineData("UPDATE \"LoyaltyActions\" SET \"StampsAfter\" = 7")]
    [InlineData("DELETE FROM \"LoyaltyActions\"")]
    public async Task Ledger_RejectsUpdateDeleteAndTruncate(string sql)
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 2);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, exception.SqlState);
        Assert.Contains("append-only", exception.MessageText);
        Assert.Equal(new LoyaltyStatusDto(2, 0, 8), await client.StatusAsync(customer));
    }

    [Fact]
    public async Task Ledger_BalanceIsAlwaysTheSumOfEntries()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var session = await client.ScanOkAsync(barista, customer);
        await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 4);
        await client.PurchaseOkAsync(barista, session.Id, Latte, 4);
        await client.PurchaseOkAsync(barista, session.Id, Latte, 3);
        await client.RedeemOkAsync(barista, session.Id, Cappuccino);
        var last = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, 4);
        await (await client.ReverseAsync(barista, last.ActionId)).ActionOkAsync();

        Assert.Equal(new LoyaltyStatusDto(3, 0, 8), await client.StatusAsync(customer));
        await AssertBalanceMatchesLedgerAsync(customer);
    }

    /// <summary>
    /// Балансът от API-то, сумата на записите и балансът, записан в последното действие, съвпадат.
    /// Поредните номера на действията са 1..n без пропуски.
    /// </summary>
    private async Task AssertBalanceMatchesLedgerAsync(TestUser customer)
    {
        var status = await client.StatusAsync(customer);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.CustomerAccounts.SingleAsync(a => a.UserId == customer.Id);
        var entries = await db.LedgerEntries.Where(e => e.AccountId == account.Id).ToListAsync();
        var actions = await db.LoyaltyActions.Where(a => a.AccountId == account.Id).OrderBy(a => a.Sequence).ToListAsync();

        Assert.Equal(status.Stamps, entries.Sum(e => e.StampDelta));
        Assert.Equal(status.Rewards, entries.Sum(e => e.RewardDelta));
        Assert.Equal((status.Stamps, status.Rewards), (actions[^1].StampsAfter, actions[^1].RewardsAfter));
        Assert.Equal(Enumerable.Range(1, actions.Count), actions.Select(a => a.Sequence));
        Assert.Equal(actions.Count, account.ActionCount);
        Assert.All(actions, a => Assert.InRange(a.StampsAfter, 0, 7));
    }

    private async Task<ScanSession> AddSessionAsync(TestUser barista, TestUser customer, DateTimeOffset openedAt)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var accountId = await db.CustomerAccounts.Where(a => a.UserId == customer.Id).Select(a => a.Id).SingleAsync();
        var session = ScanSession.Open(barista.Id, accountId, openedAt, TimeSpan.FromMinutes(2));
        db.ScanSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }
}
