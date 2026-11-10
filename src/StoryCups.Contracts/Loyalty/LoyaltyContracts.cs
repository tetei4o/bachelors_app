using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace StoryCups.Contracts.Loyalty;

/// <summary>
/// Баланс на клиента и прагът за награда. Балансът се извежда от ledger-а при всяко четене.
/// </summary>
public sealed record LoyaltyStatusDto(int Stamps, int Rewards, int StampThreshold)
{
    [JsonIgnore]
    public int StampsToNextReward => Math.Max(StampThreshold - Stamps, 0);
}

/// <summary>
/// Продукт от менюто, както го вижда баристата при избора.
/// </summary>
public sealed record ProductDto(Guid Id, string Name, int VolumeMl, decimal PriceEur, bool EarnsStamp);

/// <summary>
/// Видове действия в полето "type" на заявките и отговорите.
/// </summary>
public static class LoyaltyActionTypes
{
    /// <summary>Покупка на място: начислява печати за купените напитки.</summary>
    public const string Purchase = "purchase";

    public const string RedeemReward = "redeem_reward";

    public const string Reversal = "reversal";
}

public sealed class PurchaseItem
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; } = 1;
}

/// <summary>
/// Действие в сесия на сканиране. При "purchase" се подават <see cref="Items"/>, при "redeem_reward"
/// се подава <see cref="ProductId"/> на безплатната напитка. Заявката носи и заглавие Idempotency-Key.
/// </summary>
public sealed class LoyaltyActionRequest : IValidatableObject
{
    /// <summary>Най-много бройки в една покупка: пази от грешно въведени количества.</summary>
    public const int MaxUnits = 20;

    public const int MaxLines = 20;

    [Required(ErrorMessage = "Липсва видът на действието.")]
    public string Type { get; set; } = string.Empty;

    public List<PurchaseItem>? Items { get; set; }

    public Guid? ProductId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        switch (Type)
        {
            case LoyaltyActionTypes.Purchase:
                if (Items is not { Count: > 0 })
                {
                    yield return new ValidationResult("Изберете поне един продукт.", [nameof(Items)]);
                    yield break;
                }

                if (Items.Count > MaxLines
                    || Items.Any(item => item is null || item.ProductId == Guid.Empty || item.Quantity is < 1 or > MaxUnits))
                {
                    yield return new ValidationResult(
                        $"Всеки ред е продукт с брой от 1 до {MaxUnits}.", [nameof(Items)]);
                    yield break;
                }

                if (Items.Sum(item => item.Quantity) > MaxUnits)
                {
                    yield return new ValidationResult(
                        $"Една покупка е най-много {MaxUnits} бройки.", [nameof(Items)]);
                }

                if (ProductId is not null)
                {
                    yield return new ValidationResult(
                        "Полето productId се използва само при награда.", [nameof(ProductId)]);
                }

                break;

            case LoyaltyActionTypes.RedeemReward:
                if (ProductId is null || ProductId == Guid.Empty)
                {
                    yield return new ValidationResult("Изберете напитката за наградата.", [nameof(ProductId)]);
                }

                if (Items is { Count: > 0 })
                {
                    yield return new ValidationResult(
                        "Полето items се използва само при покупка.", [nameof(Items)]);
                }

                break;

            default:
                yield return new ValidationResult(
                    $"Видът е \"{LoyaltyActionTypes.Purchase}\" или \"{LoyaltyActionTypes.RedeemReward}\".", [nameof(Type)]);
                break;
        }
    }
}

public sealed class ReversalRequest
{
    public const int ReasonMinLength = 3;

    public const int ReasonMaxLength = 200;

    /// <summary>Защо се отменя действието. Остава в записа на отмяната.</summary>
    [Required(ErrorMessage = "Въведете причина за отмяната.")]
    [StringLength(ReasonMaxLength, MinimumLength = ReasonMinLength, ErrorMessage = "Причината е от {2} до {1} знака.")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Резултат от действие. Същият отговор се връща и при повторена заявка със същия Idempotency-Key.
/// </summary>
/// <param name="UndoUntil">До кога действието може да се отмени; празно при отмяна.</param>
/// <param name="ReversedActionId">При отмяна: кое действие е отменено.</param>
public sealed record LoyaltyActionResultDto(
    Guid ActionId,
    string Type,
    DateTimeOffset CreatedAt,
    int StampsAdded,
    int RewardsEarned,
    int RewardsRedeemed,
    LoyaltyStatusDto Loyalty,
    DateTimeOffset? UndoUntil,
    Guid? ReversedActionId);

/// <summary>
/// Ред от историята на клиента: едно действие с ефекта му върху баланса.
/// </summary>
/// <param name="Sequence">Пореден номер на действието в сметката; служи и за курсор при страниране.</param>
/// <param name="Products">Напитките, за които е действието, като текст: „Cappuccino, Latte ×2“.</param>
/// <param name="IsReversed">Действието е отменено по-късно.</param>
public sealed record LoyaltyHistoryItemDto(
    Guid ActionId,
    int Sequence,
    string Type,
    DateTimeOffset CreatedAt,
    int StampsAdded,
    int RewardsEarned,
    int RewardsRedeemed,
    string? Products,
    bool IsReversed,
    int StampsAfter,
    int RewardsAfter);

/// <param name="NextBefore">Стойност за параметъра before на следващата страница; празно, ако няма още.</param>
public sealed record LoyaltyHistoryDto(IReadOnlyList<LoyaltyHistoryItemDto> Items, int? NextBefore);
