using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoryCups.Domain.Loyalty;
using StoryCups.Domain.Qr;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.Infrastructure.Persistence.Configurations;

internal sealed class ScanSessionConfiguration : IEntityTypeConfiguration<ScanSession>
{
    public void Configure(EntityTypeBuilder<ScanSession> builder)
    {
        builder.ToTable("ScanSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasIndex(s => s.BaristaId);
        builder.HasIndex(s => s.AccountId);

        // Сесиите са следа кой кого е сканирал, затова служител и сметка със сесии не се изтриват каскадно.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.BaristaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CustomerAccount>()
            .WithMany()
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
