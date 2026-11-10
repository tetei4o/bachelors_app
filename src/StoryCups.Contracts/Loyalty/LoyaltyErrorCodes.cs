namespace StoryCups.Contracts.Loyalty;

/// <summary>
/// Кодове на грешки от QR токена, сканирането и действията с печати в полето "code" на ProblemDetails.
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

    /// <summary>Сесията липсва или е на друг бариста.</summary>
    public const string ScanSessionNotFound = "scan_session_not_found";

    public const string ScanSessionExpired = "scan_session_expired";

    /// <summary>Липсва заглавието Idempotency-Key или стойността не е UUID.</summary>
    public const string IdempotencyKeyRequired = "idempotency_key_required";

    /// <summary>Същият Idempotency-Key вече е използван за заявка с друго съдържание.</summary>
    public const string IdempotencyKeyReused = "idempotency_key_reused";

    /// <summary>Продуктът липсва или е изключен от менюто.</summary>
    public const string UnknownProduct = "unknown_product";

    /// <summary>Нито един от избраните продукти не носи печат.</summary>
    public const string NothingToRecord = "nothing_to_record";

    /// <summary>Наградата не може да се използва за този продукт.</summary>
    public const string ProductNotEligible = "product_not_eligible";

    public const string NoRewardAvailable = "no_reward_available";

    /// <summary>Друго действие по същата сметка е записано едновременно; заявката може да се повтори.</summary>
    public const string ConcurrentUpdate = "concurrent_update";

    public const string ActionNotFound = "action_not_found";

    /// <summary>Отмяна на отмяна няма.</summary>
    public const string NotReversible = "not_reversible";

    public const string AlreadyReversed = "already_reversed";

    /// <summary>Отменя само баристата, направил действието, или администратор.</summary>
    public const string ReversalForbidden = "reversal_forbidden";

    public const string UndoWindowExpired = "undo_window_expired";

    /// <summary>Отменя се само последното действие по сметката.</summary>
    public const string NotLastAction = "not_last_action";
}
