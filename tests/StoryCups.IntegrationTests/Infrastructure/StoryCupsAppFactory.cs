using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StoryCups.Infrastructure.Email;
using StoryCups.Infrastructure.Identity;
using StoryCups.Infrastructure.Seeding;
using Testcontainers.PostgreSql;

namespace StoryCups.IntegrationTests.Infrastructure;

/// <summary>
/// Пуска приложението в паметта срещу истински PostgreSQL в контейнер (Testcontainers).
/// Писмата се хващат от <see cref="CapturingEmailSender"/>.
/// </summary>
public class StoryCupsAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public CapturingEmailSender Emails { get; } = new();

    protected virtual int AuthPermitsPerMinute => 1000;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();

        // WebApplicationFactory спира Program.cs след Build(), затова миграциите се пускат оттук.
        await Services.InitializeDatabaseAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Файловете от RCL (_content/StoryCups.Shared/...) се намират по манифеста само в Development;
        // тук се включват изрично, за да се проверява, че страниците сочат към съществуващи файлове.
        builder.UseStaticWebAssets();
        builder.UseSetting("ConnectionStrings:Default", postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("App:PublicBaseUrl", "http://localhost/");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("RateLimiting:AuthPermitsPerMinute", AuthPermitsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender<ApplicationUser>>();
            services.RemoveAll<IAccountNotifier>();
            services.AddSingleton<IEmailSender<ApplicationUser>>(Emails);
            services.AddSingleton<IAccountNotifier>(Emails);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<StoryCupsAppFactory>
{
    public const string Name = "app";
}
