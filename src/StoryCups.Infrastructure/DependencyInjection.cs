using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StoryCups.Application.Auth;
using StoryCups.Application.Profile;
using StoryCups.Contracts.Auth;
using StoryCups.Infrastructure.Auth;
using StoryCups.Infrastructure.Email;
using StoryCups.Infrastructure.Identity;
using StoryCups.Infrastructure.Persistence;
using StoryCups.Infrastructure.Profile;
using StoryCups.Infrastructure.Seeding;

namespace StoryCups.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    /// <summary>
    /// Регистрира базата, Identity хранилището, имейла и услугите за токени.
    /// Схемите за автентикация (бисквитка и JWT bearer) се настройват в хоста.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Липсва ConnectionStrings:{ConnectionStringName}. Задайте го с dotnet user-secrets.");
            options.UseNpgsql(connectionString);
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.User.RequireUniqueEmail = true;

                // Дължина вместо правила за състав (OWASP Authentication Cheat Sheet).
                options.Password.RequiredLength = PasswordRules.MinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddSignInManager()
            .AddErrorDescriber<BulgarianIdentityErrorDescriber>()
            .AddDefaultTokenProviders();

        services.AddOptions<AppUrlOptions>()
            .Bind(configuration.GetSection(AppUrlOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => TryCreateBaseUri(o), "App:PublicBaseUrl трябва да е абсолютен http(s) адрес.")
            .ValidateOnStart();
        services.AddSingleton<AccountLinks>();

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<IEmailSender<ApplicationUser>>(sp => sp.GetRequiredService<SmtpEmailSender>());
        services.AddSingleton<IAccountNotifier>(sp => sp.GetRequiredService<SmtpEmailSender>());

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => TryCreateKey(o), "Jwt:SigningKey трябва да е Base64 и поне 32 байта.")
            .ValidateOnStart();
        services.AddSingleton<AccessTokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserProfileReader, UserProfileReader>();

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        return services;
    }

    private static bool TryCreateBaseUri(AppUrlOptions options)
    {
        try
        {
            options.GetBaseUri();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryCreateKey(JwtOptions options)
    {
        try
        {
            options.CreateSigningKey();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
