using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StoryCups.Domain.Identity;
using StoryCups.Domain.Loyalty;
using StoryCups.Domain.Qr;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<CustomerAccount> CustomerAccounts => Set<CustomerAccount>();

    public DbSet<UsedQrNonce> UsedQrNonces => Set<UsedQrNonce>();

    public DbSet<ScanSession> ScanSessions => Set<ScanSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
