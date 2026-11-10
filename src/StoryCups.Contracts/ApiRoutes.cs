namespace StoryCups.Contracts;

/// <summary>
/// Пътищата на REST API-то (ADR-001). Общи за сървъра и мобилното приложение.
/// </summary>
public static class ApiRoutes
{
    public const string Root = "/api/v1";

    public static class Auth
    {
        public const string Group = Root + "/auth";
        public const string Register = Group + "/register";
        public const string Login = Group + "/login";
        public const string Refresh = Group + "/refresh";
        public const string Logout = Group + "/logout";
    }

    public static class Profile
    {
        public const string Me = Root + "/me";
    }

    /// <summary>
    /// QR токен на клиента и сканиране от баристата (ADR-005, ADR-008).
    /// </summary>
    public static class Qr
    {
        public const string Tokens = Root + "/qr-tokens";
        public const string Scans = Root + "/scans";
    }

    /// <summary>
    /// Печати и награди: действия на баристата в сесия на сканиране и данните на клиента (ADR-008, ADR-015).
    /// </summary>
    public static class Loyalty
    {
        public const string Products = Root + "/products";

        /// <summary>Шаблон на пътя за действие в сесия на сканиране.</summary>
        public const string ScanActions = Qr.Scans + "/{scanId:guid}/actions";

        /// <summary>Шаблон на пътя за отмяна на действие.</summary>
        public const string ActionReversal = Root + "/actions/{actionId:guid}/reversal";

        public const string MyStatus = Profile.Me + "/loyalty";
        public const string MyHistory = Profile.Me + "/history";

        public static string ActionsFor(Guid scanId) => $"{Qr.Scans}/{scanId}/actions";

        public static string ReversalFor(Guid actionId) => $"{Root}/actions/{actionId}/reversal";
    }
}

/// <summary>
/// HTTP заглавия на API-то извън стандартните.
/// </summary>
public static class ApiHeaders
{
    /// <summary>Ключ от клиента (UUID), с който повторена заявка не се изпълнява втори път (ADR-008).</summary>
    public const string IdempotencyKey = "Idempotency-Key";

    /// <summary>Сървърът връща "true", когато отговорът е на вече изпълнена заявка със същия ключ.</summary>
    public const string IdempotentReplayed = "Idempotent-Replayed";
}
