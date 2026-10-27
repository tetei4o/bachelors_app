using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace StoryCups.Infrastructure.Auth;

/// <summary>
/// Генерира refresh токени: 256 бита от CSPRNG, кодирани base64url. В базата отива само SHA-256 хешът.
/// </summary>
public static class RefreshTokenGenerator
{
    public const int TokenBytes = 32;

    public static string Generate() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
