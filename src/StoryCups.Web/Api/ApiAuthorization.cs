namespace StoryCups.Web.Api;

/// <summary>
/// API-то приема само Bearer токени; бисквитката на сайта не важи за /api (ADR-004).
/// </summary>
internal static class ApiAuthorization
{
    public const string PolicyName = "api";
}
