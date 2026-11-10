using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoryCups.Domain.Loyalty;
using StoryCups.Infrastructure.Seeding;

namespace StoryCups.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_VolumeMl", "\"VolumeMl\" >= 1");
            table.HasCheckConstraint("CK_Products_PriceEur", "\"PriceEur\" >= 0");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(Product.NameMaxLength);
        builder.Property(p => p.PriceEur).HasPrecision(6, 2);
        builder.HasIndex(p => p.SortOrder);

        builder.HasData(MenuSeed.Drinks.Select(drink => new
        {
            drink.Id,
            drink.Name,
            drink.VolumeMl,
            drink.PriceEur,
            EarnsStamp = true,
            IsActive = true,
            drink.SortOrder,
        }));
    }
}
