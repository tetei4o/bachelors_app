using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoryCups.Domain.Loyalty;
using StoryCups.Domain.Qr;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyActionConfiguration : IEntityTypeConfiguration<LoyaltyAction>
{
    public const string IdempotencyIndexName = "IX_LoyaltyActions_ActorId_IdempotencyKey";
    public const string SequenceIndexName = "IX_LoyaltyActions_AccountId_Sequence";
    public const string ReversesIndexName = "IX_LoyaltyActions_ReversesActionId";

    public void Configure(EntityTypeBuilder<LoyaltyAction> builder)
    {
        builder.ToTable("LoyaltyActions", table =>
        {
            table.HasCheckConstraint("CK_LoyaltyActions_Sequence", "\"Sequence\" >= 1");

            // Инвариантът на правилата, повторен в базата: балансът след действие не е отрицателен.
            table.HasCheckConstraint("CK_LoyaltyActions_BalanceAfter", "\"StampsAfter\" >= 0 AND \"RewardsAfter\" >= 0");
            table.HasCheckConstraint(
                "CK_LoyaltyActions_Reversal",
                "(\"Type\" = 'Reversal') = (\"ReversesActionId\" IS NOT NULL) AND (\"Type\" = 'Reversal') = (\"Reason\" IS NOT NULL)");
        });
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.RequestHash).HasMaxLength(64).IsFixedLength();
        builder.Property(a => a.Reason).HasMaxLength(LoyaltyAction.ReasonMaxLength);
        builder.Ignore(a => a.BalanceAfter);

        // Идемпотентност (ADR-008): един ключ на един служител е най-много едно действие.
        builder.HasIndex(a => new { a.ActorId, a.IdempotencyKey }).IsUnique().HasDatabaseName(IdempotencyIndexName);

        // Поредните номера в една сметка не се повтарят: втора защита до версията на реда на сметката (ADR-015).
        builder.HasIndex(a => new { a.AccountId, a.Sequence }).IsUnique().HasDatabaseName(SequenceIndexName);

        // Едно действие се отменя най-много веднъж.
        builder.HasIndex(a => a.ReversesActionId).IsUnique().HasDatabaseName(ReversesIndexName);

        builder.HasOne<CustomerAccount>()
            .WithMany()
            .HasForeignKey(a => a.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanSession>()
            .WithMany()
            .HasForeignKey(a => a.ScanSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoyaltyAction>()
            .WithMany()
            .HasForeignKey(a => a.ReversesActionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Entries)
            .WithOne()
            .HasForeignKey(e => e.ActionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(a => a.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
