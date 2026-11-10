namespace StoryCups.Contracts.Loyalty;

/// <summary>
/// Кодове на грешки от QR токена и сканирането в полето "code" на ProblemDetails.
/// </summary>
public static class LoyaltyErrorCodes
{
    /// <summary>Кодът не е наш: грешна дължина, версия, ключ или подпис.</summary>
    public const string QrInvalid = "qr_invalid";

    public const string QrExpired = "qr_expired";

    public const string QrAlreadyUsed = "qr_already_used";

    /// <summary>Акаунтът на клиента липсва, не е потвърден или е заключен.</summary>
    public const string CustomerInactive = "customer_inactive";

    /// <summary>Служител не може да сканира собствения си код.</summary>
    public const string SelfScan = "self_scan";
}
