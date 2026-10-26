using System.Buffers.Text;
using System.Security.Claims;
using System.Text.Json;

namespace StoryCups.Mobile.Auth;

/// <summary>
/// Чете claims от access токена, за да покаже името и ролите в интерфейса.
/// Подписът не се проверява тук: проверява го сървърът при всяка заявка.
/// </summary>
internal static class AccessTokenReader
{
    public const string NameClaim = "name";
    public const string RoleClaim = "role";

    public static ClaimsPrincipal ToPrincipal(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length != 3)
        {
            throw new FormatException("Access токенът не е JWT.");
        }

        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        var claims = new List<Claim>();
        foreach (var property in payload.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                claims.AddRange(property.Value.EnumerateArray().Select(v => new Claim(property.Name, v.ToString())));
            }
            else
            {
                claims.Add(new Claim(property.Name, property.Value.ToString()));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "jwt", NameClaim, RoleClaim));
    }
}
