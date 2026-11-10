using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StoryCups.Contracts;
using StoryCups.Contracts.Loyalty;
using StoryCups.Infrastructure.Persistence;
using StoryCups.Infrastructure.Seeding;
using StoryCups.IntegrationTests.Infrastructure;

namespace StoryCups.IntegrationTests;

/// <summary>Собствена база, защото тестовете променят правилата и менюто.</summary>
public sealed class LoyaltySettingsAppFactory : StoryCupsAppFactory;

/// <summary>
/// Правилата са данни в базата (NFR-11): промяна на реда с настройките или на продукт променя поведението
/// без нова версия на приложението.
/// </summary>
public sealed class LoyaltySettingsTests(LoyaltySettingsAppFactory app) : IClassFixture<LoyaltySettingsAppFactory>
{
    private static readonly Guid Cappuccino = MenuSeed.Drinks[2].Id;
    private static readonly Guid ColdBrew = MenuSeed.Drinks[8].Id;
    private static readonly Guid V60 = MenuSeed.Drinks[7].Id;

    private readonly HttpClient client = app.CreateClient();

    [Fact]
    public async Task ChangedRules_ApplyToTheNextAction()
    {
        await ExecuteAsync("""UPDATE "LoyaltySettings" SET "StampThreshold" = 3, "MaxStampsPerTransaction" = 2, "UndoWindowMinutes" = 0""");
        try
        {
            var customer = await app.NewCustomerAsync(client);
            var barista = await app.NewStaffAsync(client);
            var session = await client.ScanOkAsync(barista, customer);
            Assert.Equal(3, session.Loyalty.StampThreshold);

            var capped = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, quantity: 5);
            var rewarded = await client.PurchaseOkAsync(barista, session.Id, Cappuccino, quantity: 1);
            var undo = await client.ReverseAsync(barista, rewarded.ActionId);

            Assert.Equal(2, capped.StampsAdded);
            Assert.Equal(new LoyaltyStatusDto(0, 1, 3), rewarded.Loyalty);
            Assert.Equal(rewarded.CreatedAt, rewarded.UndoUntil);
            Assert.Equal(HttpStatusCode.Conflict, undo.StatusCode);
            Assert.Equal(LoyaltyErrorCodes.UndoWindowExpired, await undo.ErrorCodeAsync());
            Assert.Equal(new LoyaltyStatusDto(0, 1, 3), await client.StatusAsync(customer));
        }
        finally
        {
            await ExecuteAsync("""UPDATE "LoyaltySettings" SET "StampThreshold" = 8, "MaxStampsPerTransaction" = 4, "UndoWindowMinutes" = 5""");
        }
    }

    [Fact]
    public async Task ProductFlags_DecideWhatEarnsAStampAndWhatIsOffered()
    {
        await ExecuteAsync($"""UPDATE "Products" SET "EarnsStamp" = false WHERE "Id" = '{ColdBrew}'""");
        await ExecuteAsync($"""UPDATE "Products" SET "IsActive" = false WHERE "Id" = '{V60}'""");
        try
        {
            var customer = await app.NewCustomerAsync(client);
            var barista = await app.NewStaffAsync(client);
            var session = await client.ScanOkAsync(barista, customer);

            var products = (await (await client.SendAsync(HttpMethod.Get, ApiRoutes.Loyalty.Products, barista.AccessToken))
                .Content.ReadFromJsonAsync<List<ProductDto>>())!;
            var noStamp = await client.PurchaseAsync(barista, session.Id, ColdBrew, 2);
            var mixed = await (await client.SubmitActionAsync(
                barista, session.Id, ApiClient.Purchase((ColdBrew, 2), (Cappuccino, 1)))).ActionOkAsync();
            var inactive = await client.PurchaseAsync(barista, session.Id, V60);

            Assert.Equal(11, products.Count);
            Assert.DoesNotContain(products, p => p.Id == V60);
            Assert.False(products.Single(p => p.Id == ColdBrew).EarnsStamp);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, noStamp.StatusCode);
            Assert.Equal(LoyaltyErrorCodes.NothingToRecord, await noStamp.ErrorCodeAsync());
            Assert.Equal(1, mixed.StampsAdded);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, inactive.StatusCode);
            Assert.Equal(LoyaltyErrorCodes.UnknownProduct, await inactive.ErrorCodeAsync());

            // Наградата е напитка от продуктите, които носят печат (A7).
            await ExecuteAsync("""UPDATE "LoyaltySettings" SET "StampThreshold" = 1""");
            await client.PurchaseOkAsync(barista, session.Id, Cappuccino);
            var notEligible = await client.SubmitActionAsync(barista, session.Id, ApiClient.Redeem(ColdBrew));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, notEligible.StatusCode);
            Assert.Equal(LoyaltyErrorCodes.ProductNotEligible, await notEligible.ErrorCodeAsync());
            Assert.Equal(1, (await client.RedeemOkAsync(barista, session.Id, Cappuccino)).RewardsRedeemed);
        }
        finally
        {
            await ExecuteAsync("""UPDATE "Products" SET "EarnsStamp" = true, "IsActive" = true""");
            await ExecuteAsync("""UPDATE "LoyaltySettings" SET "StampThreshold" = 8""");
        }
    }

    [Theory]
    [InlineData("UPDATE \"LoyaltySettings\" SET \"StampThreshold\" = 0")]
    [InlineData("UPDATE \"LoyaltySettings\" SET \"MaxStampsPerTransaction\" = 0")]
    [InlineData("UPDATE \"LoyaltySettings\" SET \"UndoWindowMinutes\" = -1")]
    [InlineData("INSERT INTO \"LoyaltySettings\" (\"Id\", \"StampThreshold\", \"MaxStampsPerTransaction\", \"UndoWindowMinutes\") VALUES (2, 8, 4, 5)")]
    public async Task Database_RejectsSettingsThatBreakTheRules(string sql)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sql);
    }
}
