using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StoryCups.Infrastructure.Persistence;

/// <summary>
/// Използва се само от dotnet ef при създаване на миграции; не се свързва с база.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=storycups_design")
            .Options;
        return new AppDbContext(options);
    }
}
