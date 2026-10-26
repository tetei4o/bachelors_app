using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoryCups.Domain.Identity;

namespace StoryCups.Infrastructure.Persistence.Configurations;

/// <summary>
/// Ролите се създават от миграцията с фиксирани идентификатори, за да са еднакви във всяка среда.
/// </summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public static readonly Guid CustomerRoleId = new("0195f0a0-0000-7000-8000-000000000001");
    public static readonly Guid BaristaRoleId = new("0195f0a0-0000-7000-8000-000000000002");
    public static readonly Guid AdminRoleId = new("0195f0a0-0000-7000-8000-000000000003");

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.HasData(
            Role(CustomerRoleId, AppRoles.Customer),
            Role(BaristaRoleId, AppRoles.Barista),
            Role(AdminRoleId, AppRoles.Admin));
    }

    private static IdentityRole<Guid> Role(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id.ToString(),
    };
}
