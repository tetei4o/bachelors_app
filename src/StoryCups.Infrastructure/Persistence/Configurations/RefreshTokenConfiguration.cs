using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoryCups.Domain.Identity;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        // SHA-256 в шестнадесетичен вид: 64 символа.
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.UserId);

        builder.Property(t => t.RevocationReason).HasConversion<string>().HasMaxLength(32);

        // Optimistic concurrency чрез системната колона xmin на PostgreSQL:
        // две едновременни ротации на един токен не могат да създадат два валидни наследника.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
