namespace StoryCups.Domain.Qr;

/// <summary>
/// Двоичният формат v1 на QR токена (ADR-005): 74 байта, кодирани base64url (99 символа).
/// Подредба: ver(1) kid(1) uid(16) iat(4) exp(4) nonce(16) mac(32). Числата са big-endian.
/// </summary>
public static class QrTokenFormat
{
    public const byte Version = 1;

    public const int NonceLength = 16;

    public const int MacLength = 32;

    /// <summary>Минимум 256 бита за HMAC-SHA256.</summary>
    public const int MinimumKeyBytes = 32;

    public const int VersionOffset = 0;

    public const int KeyIdOffset = 1;

    public const int PublicIdOffset = 2;

    public const int IssuedAtOffset = 18;

    public const int ExpiresAtOffset = 22;

    public const int NonceOffset = 26;

    public const int MacOffset = 42;

    public const int Length = MacOffset + MacLength;

    /// <summary>Дължината на токена като текст: 74 байта в base64url без допълване.</summary>
    public const int EncodedLength = 99;
}
