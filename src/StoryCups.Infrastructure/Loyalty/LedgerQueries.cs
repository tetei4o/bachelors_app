using Microsoft.EntityFrameworkCore;
using StoryCups.Domain.Loyalty;
using StoryCups.Infrastructure.Persistence;

namespace StoryCups.Infrastructure.Loyalty;

internal static class LedgerQueries
{
    /// <summary>
    /// Балансът на сметка: сумата на записите ѝ в ledger-а. Двете суми идват с една заявка.
    /// </summary>
    public static async Task<LoyaltyBalance> GetBalanceAsync(
        this AppDbContext db, Guid accountId, CancellationToken cancellationToken)
    {
        var sums = await db.LedgerEntries
            .Where(entry => entry.AccountId == accountId)
            .GroupBy(entry => entry.AccountId)
            .Select(group => new { Stamps = group.Sum(e => e.StampDelta), Rewards = group.Sum(e => e.RewardDelta) })
            .SingleOrDefaultAsync(cancellationToken);
        return sums is null ? LoyaltyBalance.Zero : new LoyaltyBalance(sums.Stamps, sums.Rewards);
    }

    public static Task<LoyaltySettings> GetLoyaltySettingsAsync(this AppDbContext db, CancellationToken cancellationToken) =>
        db.LoyaltySettings.AsNoTracking().SingleAsync(cancellationToken);
}
