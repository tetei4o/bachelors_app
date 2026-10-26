namespace StoryCups.Infrastructure.Seeding;

/// <summary>
/// Фиктивни акаунти за разработка и демо (A17). Паролите идват от user-secrets или променливи на средата.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public List<SeedUser> Users { get; set; } = [];
}

public sealed class SeedUser
{
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string? Password { get; set; }
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Прилага миграциите при стартиране. Включено в Development и в демото.
    /// </summary>
    public bool MigrateOnStartup { get; set; }
}
