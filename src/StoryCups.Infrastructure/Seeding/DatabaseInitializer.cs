using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoryCups.Domain.Identity;
using StoryCups.Infrastructure.Identity;
using StoryCups.Infrastructure.Persistence;

namespace StoryCups.Infrastructure.Seeding;

public static class DatabaseInitializer
{
    /// <summary>
    /// Прилага миграциите (ако е разрешено) и създава конфигурираните фиктивни акаунти.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
        {
            await provider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
        }

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = provider.GetRequiredService<TimeProvider>();
        foreach (var seed in provider.GetRequiredService<IOptions<SeedOptions>>().Value.Users)
        {
            await SeedUserAsync(seed, userManager, timeProvider, logger);
        }
    }

    private static async Task SeedUserAsync(
        SeedUser seed, UserManager<ApplicationUser> userManager, TimeProvider timeProvider, ILogger logger)
    {
        if (!AppRoles.All.Contains(seed.Role))
        {
            throw new InvalidOperationException($"Непозната роля в Seed:Users: {seed.Role}");
        }

        if (await userManager.FindByEmailAsync(seed.Email) is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(seed.Password))
        {
            logger.LogWarning("Пропускам демо акаунта {Email}: липсва парола в конфигурацията.", seed.Email);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = seed.Email,
            Email = seed.Email,
            EmailConfirmed = true,
            DisplayName = seed.DisplayName,
            CreatedAt = timeProvider.GetUtcNow(),
            PrivacyConsentAt = seed.Role == AppRoles.Customer ? timeProvider.GetUtcNow() : null,
        };

        var result = await userManager.CreateAsync(user, seed.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Демо акаунтът {seed.Email} не е създаден: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, seed.Role);
        logger.LogInformation("Създаден демо акаунт {Email} с роля {Role}.", seed.Email, seed.Role);
    }
}
