using System.Security.Claims;
using StoryCups.Infrastructure.Auth;

namespace StoryCups.Web.Api;

/// <summary>
/// API-то приема само Bearer токени; бисквитката на сайта не важи за /api (ADR-004).
/// Ролите се проверяват на сървъра при всяка заявка, не в приложението.
/// </summary>
internal static class ApiAuthorization
{
    /// <summary>Всеки влязъл потребител.</summary>
    public const string PolicyName = "api";

    /// <summary>Само клиенти: личният QR код, баланс и история.</summary>
    public const string CustomerPolicy = "api.customer";

    /// <summary>Бариста или администратор: сканиране и действия.</summary>
    public const string StaffPolicy = "api.staff";

    /// <summary>
    /// Id на потребителя от claim "sub" на access токена.
    /// </summary>
    public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirst(AccessTokenClaims.Subject)?.Value, out userId);
}
