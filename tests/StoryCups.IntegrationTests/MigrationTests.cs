using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StoryCups.Infrastructure.Persistence;
using StoryCups.IntegrationTests.Infrastructure;

namespace StoryCups.IntegrationTests;

/// <summary>
/// Миграциите върху база, в която вече има данни от предишен срез, както е на машината за разработка.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class MigrationTests(StoryCupsAppFactory app)
{
    [Fact]
    public async Task QrScanning_GivesExistingUsersDistinctPublicIds()
    {
        await using var db = NewDatabase();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

        // Състоянието след срез 1: потребители без PublicId.
        await migrator.MigrateAsync("InitialIdentity");
        for (var i = 0; i < 3; i++)
        {
            var id = Guid.CreateVersion7();
            var email = $"old-{i}@example.com";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "AspNetUsers" ("Id", "DisplayName", "CreatedAt", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
                    "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
                VALUES ({id}, {"Стар Потребител"}, now(), {email}, {email.ToUpperInvariant()}, {email}, {email.ToUpperInvariant()},
                    true, false, false, true, 0)
                """);
        }

        await migrator.MigrateAsync();

        var publicIds = await db.Users.Select(u => u.PublicId).ToListAsync();
        Assert.Equal(3, publicIds.Count);
        Assert.Equal(3, publicIds.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, publicIds);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task LoyaltyLedger_AppliesOverDataFromTheScanningSlice_AndCanBeRolledBack()
    {
        await using var db = NewDatabase();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

        // Състоянието след срез 2: клиент със сметка и сесия на сканиране, без таблиците на ledger-а.
        await migrator.MigrateAsync("QrScanning");
        var customerId = Guid.CreateVersion7();
        var baristaId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        foreach (var (id, email) in new[] { (customerId, "old-customer@example.com"), (baristaId, "old-barista@example.com") })
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "AspNetUsers" ("Id", "DisplayName", "CreatedAt", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
                    "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
                VALUES ({id}, {"Стар Потребител"}, now(), {email}, {email.ToUpperInvariant()}, {email}, {email.ToUpperInvariant()},
                    true, false, false, true, 0)
                """);
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "CustomerAccounts" ("Id", "UserId", "ActionCount", "CreatedAt") VALUES ({accountId}, {customerId}, 0, now());
            INSERT INTO "ScanSessions" ("Id", "BaristaId", "AccountId", "CreatedAt", "ExpiresAt")
            VALUES ({Guid.CreateVersion7()}, {baristaId}, {accountId}, now(), now() + interval '2 minutes');
            """);

        await migrator.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(12, await db.Products.CountAsync(p => p.IsActive && p.EarnsStamp));
        var settings = await db.LoyaltySettings.SingleAsync();
        Assert.Equal((8, 4, 5, null), (settings.StampThreshold, settings.MaxStampsPerTransaction, settings.UndoWindowMinutes, settings.StampValidityDays));
        Assert.Equal(accountId, (await db.CustomerAccounts.SingleAsync()).Id);
        Assert.Equal(1, await db.ScanSessions.CountAsync());

        // Връщане към срез 2 и отново напред: Down маха таблиците и функцията на тригерите.
        await migrator.MigrateAsync("QrScanning");
        Assert.Equal(1, await db.CustomerAccounts.CountAsync());
        await migrator.MigrateAsync();
        Assert.Equal(12, await db.Products.CountAsync());
    }

    private AppDbContext NewDatabase()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(app.ConnectionString)
        {
            Database = $"migration_{Guid.NewGuid():N}",
        }.ConnectionString;
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
    }
}
