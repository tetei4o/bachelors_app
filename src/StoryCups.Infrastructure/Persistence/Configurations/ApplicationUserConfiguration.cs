using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.DisplayName)
            .HasMaxLength(ApplicationUser.DisplayNameMaxLength)
            .IsRequired();

        // Стойността идва от приложението (Guid.NewGuid). Подразбирането в базата попълва съществуващите редове
        // при миграцията и пази колоната от празна стойност при запис извън приложението.
        builder.Property(u => u.PublicId).HasDefaultValueSql("gen_random_uuid()");
        builder.HasIndex(u => u.PublicId).IsUnique();
    }
}
