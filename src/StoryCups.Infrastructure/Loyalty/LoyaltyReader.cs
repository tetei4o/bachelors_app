using Microsoft.EntityFrameworkCore;
using StoryCups.Application.Loyalty;
using StoryCups.Contracts.Loyalty;
using StoryCups.Domain.Loyalty;
using StoryCups.Infrastructure.Persistence;

namespace StoryCups.Infrastructure.Loyalty;

/// <summary>
/// Баланс и история на клиент. Клиентът вижда какво се е случило с картата му, но не и кой служител го е направил.
/// </summary>
public sealed class LoyaltyReader(AppDbContext db) : ILoyaltyReader
{
    public const int MaxPageSize = 50;

    public async Task<LoyaltyStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var settings = await db.GetLoyaltySettingsAsync(cancellationToken);
        var accountId = await FindAccountIdAsync(userId, cancellationToken);
        var balance = accountId is null
            ? LoyaltyBalance.Zero
            : await db.GetBalanceAsync(accountId.Value, cancellationToken);
        return new LoyaltyStatusDto(balance.Stamps, balance.Rewards, settings.StampThreshold);
    }

    public async Task<LoyaltyHistoryDto> GetHistoryAsync(
        Guid userId, int limit, int? before = null, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, MaxPageSize);
        var accountId = await FindAccountIdAsync(userId, cancellationToken);
        if (accountId is null)
        {
            return new LoyaltyHistoryDto([], null);
        }

        var query = db.LoyaltyActions.AsNoTracking()
            .Include(action => action.Entries)
            .Where(action => action.AccountId == accountId);
        if (before is not null)
        {
            query = query.Where(action => action.Sequence < before);
        }

        // Един ред повече от страницата показва дали има следваща.
        var actions = await query
            .OrderByDescending(action => action.Sequence)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var page = actions.Take(limit).ToList();

        var actionIds = page.Select(action => action.Id).ToList();
        var reversedIds = (await db.LoyaltyActions
                .Where(action => action.ReversesActionId != null && actionIds.Contains(action.ReversesActionId.Value))
                .Select(action => action.ReversesActionId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var productIds = page
            .SelectMany(action => action.Entries)
            .Where(entry => entry.ProductId is not null)
            .Select(entry => entry.ProductId!.Value)
            .Distinct()
            .ToList();
        var productNames = await db.Products.AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, product => product.Name, cancellationToken);

        var items = page
            .Select(action => new LoyaltyHistoryItemDto(
                action.Id,
                action.Sequence,
                LoyaltyActionService.TypeName(action.Type),
                action.CreatedAt,
                StampsAdded: action.Entries.Where(e => e.Kind == LedgerEntryKind.Purchase).Sum(e => e.StampDelta),
                RewardsEarned: action.Entries.Where(e => e.Kind == LedgerEntryKind.RewardEarned).Sum(e => e.RewardDelta),
                RewardsRedeemed: -action.Entries.Where(e => e.Kind == LedgerEntryKind.RewardRedeemed).Sum(e => e.RewardDelta),
                Products: DescribeProducts(action, productNames),
                IsReversed: reversedIds.Contains(action.Id),
                action.StampsAfter,
                action.RewardsAfter))
            .ToList();

        return new LoyaltyHistoryDto(items, actions.Count > limit ? page[^1].Sequence : null);
    }

    /// <summary>
    /// „Cappuccino, Latte ×2“: напитките на действието в реда, в който са записани. За отмяна няма списък.
    /// </summary>
    private static string? DescribeProducts(LoyaltyAction action, Dictionary<Guid, string> productNames)
    {
        if (action.Type == LoyaltyActionType.Reversal)
        {
            return null;
        }

        var parts = action.Entries
            .Where(entry => entry.ProductId is not null)
            .OrderBy(entry => entry.Line)
            .GroupBy(entry => entry.ProductId!.Value)
            .Select(group =>
            {
                var name = productNames.GetValueOrDefault(group.Key, "?");
                return group.Count() > 1 ? $"{name} ×{group.Count()}" : name;
            })
            .ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private Task<Guid?> FindAccountIdAsync(Guid userId, CancellationToken cancellationToken) =>
        db.CustomerAccounts
            .Where(account => account.UserId == userId)
            .Select(account => (Guid?)account.Id)
            .SingleOrDefaultAsync(cancellationToken);
}

/// <summary>
/// Менюто и прагът за награда от базата (NFR-11).
/// </summary>
public sealed class ProductCatalog(AppDbContext db) : IProductCatalog
{
    public async Task<IReadOnlyList<ProductDto>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await db.Products.AsNoTracking()
            .Where(product => product.IsActive)
            .OrderBy(product => product.SortOrder)
            .ThenBy(product => product.Name)
            .Select(product => new ProductDto(
                product.Id, product.Name, product.VolumeMl, product.PriceEur, product.EarnsStamp))
            .ToListAsync(cancellationToken);

    public async Task<int> GetStampThresholdAsync(CancellationToken cancellationToken = default) =>
        (await db.GetLoyaltySettingsAsync(cancellationToken)).StampThreshold;
}
