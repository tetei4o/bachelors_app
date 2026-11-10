using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoryCups.Domain.Loyalty;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.Infrastructure.Persistence.Configurations;

internal sealed class CustomerAccountConfiguration : IEntityTypeConfiguration<CustomerAccount>
{
    public void Configure(EntityTypeBuilder<CustomerAccount> builder)
    {
        builder.ToTable("CustomerAccounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // Един потребител има най-много една сметка. Колоната допуска NULL: при изтриване на акаунта връзката
        // към човека се прекъсва, а сметката и записите ѝ остават анонимни.
        builder.HasIndex(a => a.UserId).IsUnique();
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Optimistic concurrency чрез системната колона xmin на PostgreSQL, както при refresh токените.
        builder.Property<uint>("Version").IsRowVersion();
    }
}
