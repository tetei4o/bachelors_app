using StoryCups.Contracts.Loyalty;

namespace StoryCups.Application.Loyalty;

/// <summary>
/// Четене на баланса и историята на клиент. Балансът се смята от ledger-а, не се пази.
/// </summary>
public interface ILoyaltyReader
{
    /// <summary>Клиент без нито едно сканиране още няма сметка: балансът му е нула.</summary>
    Task<LoyaltyStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <param name="before">Връща действията с пореден номер под тази стойност; празно за най-новите.</param>
    Task<LoyaltyHistoryDto> GetHistoryAsync(
        Guid userId, int limit, int? before = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Менюто и правилата на програмата, както са записани в базата (NFR-11).
/// </summary>
public interface IProductCatalog
{
    /// <summary>Активните продукти в реда от менюто.</summary>
    Task<IReadOnlyList<ProductDto>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Колко печата носят една награда.</summary>
    Task<int> GetStampThresholdAsync(CancellationToken cancellationToken = default);
}
