using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;

namespace StoryCups.Domain.Qr;

/// <summary>
/// Резултат от проверката на QR токен. Редът на стойностите следва реда на проверките в ADR-005.
/// </summary>
public enum QrTokenStatus
{
    Valid = 0,

    /// <summary>Грешна дължина, кодиране или версия на формата.</summary>
    Malformed = 1,

    /// <summary>Идентификаторът на ключа не е сред конфигурираните.</summary>
    UnknownKey = 2,

    /// <summary>Подписът не съвпада: токенът е подправен или е от друг сървър.</summary>
    BadSignature = 3,

    Expired = 4,
}

/// <summary>
/// Съдържанието на проверен токен. PublicId е случайният публичен идентификатор на клиента, не вътрешният ключ.
/// </summary>
public sealed record QrTokenPayload(byte KeyId, Guid PublicId, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, byte[] Nonce);

public readonly record struct QrTokenValidation(QrTokenStatus Status, QrTokenPayload? Payload)
{
    public bool IsValid => Status == QrTokenStatus.Valid;
}

public readonly record struct IssuedQrToken(string Token, QrTokenPayload Payload);

/// <summary>
/// Издава и проверява QR токени (ADR-005). Чиста логика без база и без часовник: времето идва като параметър.
/// Проверката на еднократността на nonce е в базата и не е тук.
/// </summary>
public sealed class QrTokenCodec
{
    private readonly Dictionary<byte, byte[]> keys;
    private readonly byte activeKeyId;

    /// <param name="keys">Ключовете по идентификатор. При ротация тук стоят новият и старият ключ.</param>
    /// <param name="activeKeyId">Ключът, с който се подписват новите токени.</param>
    public QrTokenCodec(IReadOnlyDictionary<byte, byte[]> keys, byte activeKeyId)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (!keys.ContainsKey(activeKeyId))
        {
            throw new ArgumentException($"Няма ключ с идентификатор {activeKeyId}.", nameof(activeKeyId));
        }

        foreach (var (id, key) in keys)
        {
            if (key is null || key.Length < QrTokenFormat.MinimumKeyBytes)
            {
                throw new ArgumentException(
                    $"Ключ {id} трябва да е поне {QrTokenFormat.MinimumKeyBytes} байта.", nameof(keys));
            }
        }

        this.keys = keys.ToDictionary(pair => pair.Key, pair => (byte[])pair.Value.Clone());
        this.activeKeyId = activeKeyId;
    }

    /// <summary>
    /// Издава токен със случаен nonce от криптографски сигурен генератор.
    /// </summary>
    public IssuedQrToken Issue(Guid publicId, DateTimeOffset now, TimeSpan lifetime) =>
        Issue(publicId, now, lifetime, RandomNumberGenerator.GetBytes(QrTokenFormat.NonceLength));

    /// <summary>
    /// Издава токен със зададен nonce. Отделен метод, за да са тестовете детерминирани.
    /// </summary>
    public IssuedQrToken Issue(Guid publicId, DateTimeOffset now, TimeSpan lifetime, ReadOnlySpan<byte> nonce)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Животът на токена трябва да е положителен.");
        }

        if (nonce.Length != QrTokenFormat.NonceLength)
        {
            throw new ArgumentException($"Nonce трябва да е {QrTokenFormat.NonceLength} байта.", nameof(nonce));
        }

        // Времето е в цели секунди: частта под секундата се отрязва и при издаване, и при проверка.
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        var expiresAt = issuedAt.Add(lifetime);

        Span<byte> buffer = stackalloc byte[QrTokenFormat.Length];
        buffer[QrTokenFormat.VersionOffset] = QrTokenFormat.Version;
        buffer[QrTokenFormat.KeyIdOffset] = activeKeyId;
        publicId.TryWriteBytes(buffer[QrTokenFormat.PublicIdOffset..], bigEndian: true, out _);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[QrTokenFormat.IssuedAtOffset..], checked((uint)issuedAt.ToUnixTimeSeconds()));
        BinaryPrimitives.WriteUInt32BigEndian(buffer[QrTokenFormat.ExpiresAtOffset..], checked((uint)expiresAt.ToUnixTimeSeconds()));
        nonce.CopyTo(buffer[QrTokenFormat.NonceOffset..]);
        HMACSHA256.HashData(keys[activeKeyId], buffer[..QrTokenFormat.MacOffset], buffer[QrTokenFormat.MacOffset..]);

        var payload = new QrTokenPayload(activeKeyId, publicId, issuedAt, expiresAt, nonce.ToArray());
        return new IssuedQrToken(Base64Url.EncodeToString(buffer), payload);
    }

    /// <summary>
    /// Проверява токена в реда от ADR-005: дължина и версия, познат ключ, подпис, срок.
    /// Нито една от тези стъпки не изисква заявка към базата.
    /// </summary>
    public QrTokenValidation Validate(string? token, DateTimeOffset now)
    {
        // Съдържанието идва от камерата и може да е всякакъв текст. Знаците се проверяват преди декодирането,
        // защото Base64Url.TryDecodeFromChars хвърля изключение при знак извън азбуката.
        if (token is null || token.Length != QrTokenFormat.EncodedLength || !Base64Url.IsValid(token))
        {
            return new QrTokenValidation(QrTokenStatus.Malformed, null);
        }

        Span<byte> buffer = stackalloc byte[QrTokenFormat.Length];
        if (!Base64Url.TryDecodeFromChars(token, buffer, out var written) || written != QrTokenFormat.Length)
        {
            return new QrTokenValidation(QrTokenStatus.Malformed, null);
        }

        if (buffer[QrTokenFormat.VersionOffset] != QrTokenFormat.Version)
        {
            return new QrTokenValidation(QrTokenStatus.Malformed, null);
        }

        var keyId = buffer[QrTokenFormat.KeyIdOffset];
        if (!keys.TryGetValue(keyId, out var key))
        {
            return new QrTokenValidation(QrTokenStatus.UnknownKey, null);
        }

        // Сравнение за постоянно време: отговорът не издава на коя позиция е първата разлика.
        Span<byte> expectedMac = stackalloc byte[QrTokenFormat.MacLength];
        HMACSHA256.HashData(key, buffer[..QrTokenFormat.MacOffset], expectedMac);
        if (!CryptographicOperations.FixedTimeEquals(expectedMac, buffer[QrTokenFormat.MacOffset..]))
        {
            return new QrTokenValidation(QrTokenStatus.BadSignature, null);
        }

        var payload = new QrTokenPayload(
            keyId,
            new Guid(buffer.Slice(QrTokenFormat.PublicIdOffset, 16), bigEndian: true),
            DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32BigEndian(buffer[QrTokenFormat.IssuedAtOffset..])),
            DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32BigEndian(buffer[QrTokenFormat.ExpiresAtOffset..])),
            buffer.Slice(QrTokenFormat.NonceOffset, QrTokenFormat.NonceLength).ToArray());

        // Издаването и проверката са на един сървър, затова няма толеранс за разлика в часовниците.
        return now >= payload.ExpiresAt
            ? new QrTokenValidation(QrTokenStatus.Expired, payload)
            : new QrTokenValidation(QrTokenStatus.Valid, payload);
    }
}
