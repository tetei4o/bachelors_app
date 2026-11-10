using System.ComponentModel.DataAnnotations;

namespace StoryCups.Contracts.Loyalty;

public sealed class ScanRequest
{
    /// <summary>Съдържанието на QR кода, както го е прочел скенерът.</summary>
    [Required(ErrorMessage = "Липсва съдържанието на QR кода.")]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Сесия на сканиране (ADR-008). Баристата вижда само инициалите на клиента и баланса му (A19).
/// </summary>
public sealed record ScanSessionDto(
    Guid Id, DateTimeOffset ExpiresAt, int LifetimeSeconds, string CustomerInitials, LoyaltyStatusDto Loyalty);
