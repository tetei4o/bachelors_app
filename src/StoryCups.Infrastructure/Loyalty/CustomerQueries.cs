using StoryCups.Infrastructure.Identity;
using StoryCups.Infrastructure.Persistence;
using StoryCups.Infrastructure.Persistence.Configurations;

namespace StoryCups.Infrastructure.Loyalty;

internal static class CustomerQueries
{
    /// <summary>
    /// Активен клиент: потвърден имейл, без временно заключване и с роля Customer.
    /// Едно и също условие при издаване на QR токен и при сканиране (ADR-005, стъпка 4).
    /// </summary>
    public static IQueryable<ApplicationUser> ActiveCustomers(this AppDbContext db, DateTimeOffset now) =>
        db.Users.Where(user =>
            user.EmailConfirmed
            && (user.LockoutEnd == null || user.LockoutEnd <= now)
            && db.UserRoles.Any(role => role.UserId == user.Id && role.RoleId == RoleConfiguration.CustomerRoleId));
}
