using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StoryCups.Domain.Loyalty;

/// <summary>
/// Отпечатък на заявка за действие: SHA-256 върху каноничен запис на съдържанието. Пази се до идемпотентния ключ.
/// Повторена заявка със същия ключ и същия отпечатък получава първия резултат; същият ключ с друг отпечатък
/// е грешка на клиента и се отхвърля (ADR-008).
/// </summary>
public static class ActionFingerprint
{
    /// <summary>
    /// Редовете се подреждат по продукт и повторенията на един продукт се събират, така че редът в заявката
    /// не променя отпечатъка.
    /// </summary>
    public static string ForPurchase(Guid scanSessionId, IEnumerable<(Guid ProductId, int Quantity)> lines)
    {
        var merged = lines
            .GroupBy(line => line.ProductId)
            .OrderBy(group => group.Key)
            .Select(group => string.Create(CultureInfo.InvariantCulture, $"{group.Key:N}x{group.Sum(line => line.Quantity)}"));
        return Hash($"purchase|{scanSessionId:N}|{string.Join(',', merged)}");
    }

    public static string ForRedemption(Guid scanSessionId, Guid productId) =>
        Hash($"redeem_reward|{scanSessionId:N}|{productId:N}");

    public static string ForReversal(Guid actionId, string reason) =>
        Hash($"reversal|{actionId:N}|{reason}");

    private static string Hash(string canonical) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}
