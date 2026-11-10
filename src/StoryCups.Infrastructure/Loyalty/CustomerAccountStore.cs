using Microsoft.EntityFrameworkCore;
using StoryCups.Infrastructure.Persistence;

namespace StoryCups.Infrastructure.Loyalty;

internal static class CustomerAccountStore
{
    /// <summary>
    /// Връща Id на сметката на потребителя и я създава, ако липсва: сметката се открива при първото сканиране,
    /// не при регистрацията. INSERT ... ON CONFLICT DO NOTHING е една заявка без изключение при едновременни
    /// сканирания на един клиент; уникалният индекс по UserId решава кой печели.
    /// </summary>
    public static async Task<Guid> EnsureCustomerAccountAsync(
        this AppDbContext db, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7(now);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "CustomerAccounts" ("Id", "UserId", "ActionCount", "CreatedAt")
            VALUES ({id}, {userId}, 0, {now})
            ON CONFLICT ("UserId") DO NOTHING
            """,
            cancellationToken);

        return await db.CustomerAccounts
            .Where(account => account.UserId == userId)
            .Select(account => account.Id)
            .SingleAsync(cancellationToken);
    }
}
