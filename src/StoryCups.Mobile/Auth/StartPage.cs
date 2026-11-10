using System.Security.Claims;

namespace StoryCups.Mobile.Auth;

/// <summary>
/// Първият екран след вход: баристата започва от сканирането, клиентът от картата си.
/// Ролите идват от access токена само за интерфейса; правата се проверяват от сървъра при всяка заявка.
/// </summary>
internal static class StartPage
{
    public static string For(ClaimsPrincipal user)
    {
        if (user.IsInRole("Barista") || user.IsInRole("Admin"))
        {
            return "barista";
        }

        return user.IsInRole("Customer") ? "card" : "profile";
    }
}
