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

    private AppDbContext NewDatabase()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(app.ConnectionString)
        {
            Database = $"migration_{Guid.NewGuid():N}",
        }.ConnectionString;
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
    }
}
