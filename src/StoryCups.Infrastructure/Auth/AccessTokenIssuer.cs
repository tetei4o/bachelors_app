using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.Infrastructure.Auth;

/// <summary>
/// Издава JWT access токени, подписани с HMAC-SHA256 (ADR-004).
/// </summary>
public sealed class AccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private readonly JwtOptions options = options.Value;
    private readonly JsonWebTokenHandler handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Issue(ApplicationUser user, IEnumerable<string> roles)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(options.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(AccessTokenClaims.Subject, user.Id.ToString()),
            new(AccessTokenClaims.Email, user.Email ?? string.Empty),
            new(AccessTokenClaims.Name, user.DisplayName),
            new(AccessTokenClaims.TokenId, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(roles.Select(role => new Claim(AccessTokenClaims.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        };

        return (handler.CreateToken(descriptor), expiresAt);
    }
}
