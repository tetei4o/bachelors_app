namespace StoryCups.Domain.Identity;

/// <summary>
/// Refresh токен на мобилното приложение (ADR-004).
/// В базата се пази само SHA-256 хешът на токена. Всеки токен принадлежи на верига (FamilyId):
/// при ротация новият токен наследява веригата, а повторно използван стар токен отнема цялата верига.
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public RefreshTokenRevocationReason? RevocationReason { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Issue(Guid userId, Guid familyId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Животът на токена трябва да е положителен.");
        }

        return new RefreshToken
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        };
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    /// <summary>
    /// Маркира токена като заменен при ротация.
    /// </summary>
    public void MarkReplaced(Guid replacementId, DateTimeOffset now)
    {
        if (!IsActive(now))
        {
            throw new InvalidOperationException("Само активен токен може да бъде заменен.");
        }

        RevokedAt = now;
        RevocationReason = RefreshTokenRevocationReason.Rotated;
        ReplacedByTokenId = replacementId;
    }

    /// <summary>
    /// Отнема токена. Повторното отнемане не променя първата причина.
    /// </summary>
    public void Revoke(DateTimeOffset now, RefreshTokenRevocationReason reason)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = now;
        RevocationReason = reason;
    }
}

public enum RefreshTokenRevocationReason
{
    Rotated = 1,
    Logout = 2,
    ReuseDetected = 3,
    UserDeactivated = 4,
    PasswordReset = 5,
}
