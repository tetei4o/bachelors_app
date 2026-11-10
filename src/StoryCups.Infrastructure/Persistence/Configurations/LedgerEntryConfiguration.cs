using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoryCups.Domain.Loyalty;

namespace StoryCups.Infrastructure.Persistence.Configurations;

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        // Таблицата е append-only: UPDATE, DELETE и TRUNCATE се отхвърлят от тригер, създаден в миграцията (ADR-015).
        builder.ToTable("LedgerEntries", table =>
        {
            table.HasCheckConstraint("CK_LedgerEntries_NonZero", "\"StampDelta\" <> 0 OR \"RewardDelta\" <> 0");
            table.HasCheckConstraint("CK_LedgerEntries_Line", "\"Line\" >= 1");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(32);

        // Балансът е SUM по сметка.
        builder.HasIndex(e => e.AccountId);

        // Записите на едно действие са номерирани 1..n; индексът замества и този по ActionId.
        builder.HasIndex(e => new { e.ActionId, e.Line }).IsUnique();

        // Един запис се обръща най-много веднъж.
        builder.HasIndex(e => e.ReversesEntryId).IsUnique();

        builder.HasOne<CustomerAccount>()
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerEntry>()
            .WithMany()
            .HasForeignKey(e => e.ReversesEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
