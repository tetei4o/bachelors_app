namespace StoryCups.Contracts.Auth;

/// <summary>
/// Двойка токени за мобилното приложение (ADR-004).
/// </summary>
public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
