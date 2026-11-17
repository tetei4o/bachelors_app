namespace StoryCups.Web.Components.Account;

/// <summary>
/// Ограничение на честотата за формите на страниците за акаунта (NFR-02, ADR-010). Политиката се включва
/// за всички страници в папката <c>Account/Pages</c> и се настройва в <c>Program.cs</c>.
/// </summary>
internal static class AccountRateLimiting
{
    public const string PolicyName = "account-forms";

    /// <summary>Страницата, към която се пренасочва отказана форма.</summary>
    public const string RejectedPath = "/Account/TooManyRequests";
}
