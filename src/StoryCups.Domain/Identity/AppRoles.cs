namespace StoryCups.Domain.Identity;

/// <summary>
/// Роли в системата. Проверяват се само на сървъра.
/// </summary>
public static class AppRoles
{
    public const string Customer = "Customer";
    public const string Barista = "Barista";
    public const string Admin = "Admin";

    public static IReadOnlyList<string> All { get; } = [Customer, Barista, Admin];
}
