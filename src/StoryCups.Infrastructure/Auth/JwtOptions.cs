using System.ComponentModel.DataAnnotations;
using Microsoft.IdentityModel.Tokens;

namespace StoryCups.Infrastructure.Auth;

/// <summary>
/// Параметри на JWT access токените и refresh токените (ADR-004).
/// Ключът се задава в user-secrets или в променлива на средата, никога в кода.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Минимум 256 бита за HMAC-SHA256.
    /// </summary>
    public const int MinimumKeyBytes = 32;

    [Required]
    public string Issuer { get; set; } = "StoryCups";

    [Required]
    public string Audience { get; set; } = "StoryCups.Mobile";

    /// <summary>
    /// Ключ за подпис, кодиран в Base64. Генерира се например с: openssl rand -base64 32
    /// </summary>
    [Required(ErrorMessage = "Липсва Jwt:SigningKey. Задайте го с dotnet user-secrets.")]
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    public SymmetricSecurityKey CreateSigningKey()
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(SigningKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Jwt:SigningKey трябва да е в Base64.", ex);
        }

        if (bytes.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException($"Jwt:SigningKey трябва да е поне {MinimumKeyBytes} байта.");
        }

        return new SymmetricSecurityKey(bytes);
    }

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateSigningKey(),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = AccessTokenClaims.Name,
        RoleClaimType = AccessTokenClaims.Role,
    };
}

/// <summary>
/// Имената на claims в access токена.
/// </summary>
public static class AccessTokenClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string TokenId = "jti";
}
