using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoryCups.Domain.Loyalty;

namespace StoryCups.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltySettingsConfiguration : IEntityTypeConfiguration<LoyaltySettings>
{
    public void Configure(EntityTypeBuilder<LoyaltySettings> builder)
    {
        // Един ред с правилата. Ограниченията са в базата, защото редът може да се промени и извън приложението.
        builder.ToTable("LoyaltySettings", table =>
        {
            table.HasCheckConstraint("CK_LoyaltySettings_Singleton", $"\"Id\" = {LoyaltySettings.SingletonId}");
            table.HasCheckConstraint("CK_LoyaltySettings_StampThreshold", "\"StampThreshold\" >= 1");
            table.HasCheckConstraint("CK_LoyaltySettings_MaxStampsPerTransaction", "\"MaxStampsPerTransaction\" >= 1");
            table.HasCheckConstraint("CK_LoyaltySettings_UndoWindowMinutes", "\"UndoWindowMinutes\" >= 0");
            table.HasCheckConstraint(
                "CK_LoyaltySettings_StampValidityDays", "\"StampValidityDays\" IS NULL OR \"StampValidityDays\" >= 1");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasData(new
        {
            Id = LoyaltySettings.SingletonId,
            StampThreshold = LoyaltySettings.DefaultStampThreshold,
            MaxStampsPerTransaction = LoyaltySettings.DefaultMaxStampsPerTransaction,
            UndoWindowMinutes = LoyaltySettings.DefaultUndoWindowMinutes,
            StampValidityDays = (int?)null,
        });
    }
}
