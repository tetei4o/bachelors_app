using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoryCups.Domain.Qr;

namespace StoryCups.Infrastructure.Persistence.Configurations;

internal sealed class UsedQrNonceConfiguration : IEntityTypeConfiguration<UsedQrNonce>
{
    /// <summary>По това име услугата за сканиране разпознава „кодът вече е използван“.</summary>
    public const string PrimaryKeyName = "PK_UsedQrNonces";

    public void Configure(EntityTypeBuilder<UsedQrNonce> builder)
    {
        builder.ToTable("UsedQrNonces");

        // Първичният ключ е самият nonce: еднократността се пази от базата, не от проверка в кода.
        builder.HasKey(n => n.Nonce).HasName(PrimaryKeyName);
        builder.Property(n => n.Nonce).HasMaxLength(QrTokenFormat.NonceLength).ValueGeneratedNever();

        // За периодичното изтриване на изтеклите редове.
        builder.HasIndex(n => n.ExpiresAt);
    }
}
