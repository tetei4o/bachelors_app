using StoryCups.Contracts.Loyalty;

namespace StoryCups.Shared.Services;

/// <summary>
/// Текстовете за печати и награди, общи за картата на клиента и екрана на баристата.
/// </summary>
public static class LoyaltyTexts
{
    public static string Stamps(int count) => count == 1 ? "1 печат" : $"{count} печата";

    public static string Rewards(int count) => count == 1 ? "1 безплатна напитка" : $"{count} безплатни напитки";

    /// <summary>Надписът под картата с печати.</summary>
    public static string CardCaption(LoyaltyStatusDto status) =>
        status.StampsToNextReward == 1
            ? "Още 1 печат\nдо безплатна напитка"
            : $"Още {status.StampsToNextReward} печата\nдо безплатна напитка";

    /// <summary>Какво е направило едно действие, с думи: за резултата при баристата и за историята на клиента.</summary>
    public static string Describe(string type, int stampsAdded, int rewardsEarned, int rewardsRedeemed) => type switch
    {
        LoyaltyActionTypes.Purchase when rewardsEarned > 0 =>
            $"+{Stamps(stampsAdded)}, нова награда: {Rewards(rewardsEarned)}",
        LoyaltyActionTypes.Purchase => $"+{Stamps(stampsAdded)}",
        LoyaltyActionTypes.RedeemReward => rewardsRedeemed == 1
            ? "Използвана награда"
            : $"Използвани награди: {rewardsRedeemed}",
        LoyaltyActionTypes.Reversal => "Отмяна на предишното действие",
        _ => type,
    };
}
