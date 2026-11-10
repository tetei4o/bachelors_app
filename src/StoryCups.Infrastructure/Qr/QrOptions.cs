using Microsoft.Extensions.Options;
using StoryCups.Domain.Qr;
using StoryCups.Infrastructure.Auth;

namespace StoryCups.Infrastructure.Qr;

/// <summary>
/// Параметри на QR токена и на сесията на сканиране (ADR-005, ADR-006, ADR-008).
/// Ключовете се задават в user-secrets или в променливи на средата, никога в кода.
/// </summary>
public sealed class QrOptions
{
    public const string SectionName = "Qr";

    /// <summary>Допустим живот на токена по ADR-006: от 30 до 120 секунди.</summary>
    public static readonly TimeSpan MinTokenLifetime = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan MaxTokenLifetime = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Ключовете за подпис. При ротация тук стоят новият и старият ключ; старият се маха след един живот на токена.
    /// </summary>
    public List<QrKeyOptions> Keys { get; set; } = [];

    /// <summary>Идентификаторът на ключа, с който се подписват новите токени.</summary>
    public byte ActiveKeyId { get; set; } = 1;

    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Клиентът иска нов токен, когато до изтичането остава по-малко от това време.</summary>
    public TimeSpan RefreshBeforeExpiry { get; set; } = TimeSpan.FromSeconds(20);

    public TimeSpan ScanSessionLifetime { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Колко често се изтриват изтеклите nonce.</summary>
    public TimeSpan NonceCleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    public QrTokenCodec CreateCodec() =>
        new(Keys.ToDictionary(key => key.Id, key => Convert.FromBase64String(key.Key)), ActiveKeyId);
}

public sealed class QrKeyOptions
{
    /// <summary>Един байт в токена (поле kid).</summary>
    public byte Id { get; set; }

    /// <summary>Ключ, кодиран в Base64, поне 32 байта. Генерира се например с: openssl rand -base64 32</summary>
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Проверява настройките при стартиране, за да не тръгне сървърът с липсващ, къс или повторно използван ключ.
/// </summary>
internal sealed class QrOptionsValidator(IOptions<JwtOptions> jwtOptions) : IValidateOptions<QrOptions>
{
    public ValidateOptionsResult Validate(string? name, QrOptions options)
    {
        var errors = new List<string>();

        if (options.Keys.Count == 0)
        {
            errors.Add("Липсва Qr:Keys:0:Key. Задайте го с dotnet user-secrets.");
        }

        if (options.Keys.Select(key => key.Id).Distinct().Count() != options.Keys.Count)
        {
            errors.Add("Идентификаторите в Qr:Keys трябва да са различни.");
        }

        if (options.Keys.Count > 0 && options.Keys.All(key => key.Id != options.ActiveKeyId))
        {
            errors.Add($"Qr:ActiveKeyId = {options.ActiveKeyId} не е сред Qr:Keys.");
        }

        var jwtKey = TryDecode(jwtOptions.Value.SigningKey);
        for (var index = 0; index < options.Keys.Count; index++)
        {
            var key = options.Keys[index];
            if (string.IsNullOrWhiteSpace(key.Key))
            {
                errors.Add($"Липсва Qr:Keys:{index}:Key. Задайте го с dotnet user-secrets.");
                continue;
            }

            var bytes = TryDecode(key.Key);
            if (bytes is null || bytes.Length < QrTokenFormat.MinimumKeyBytes)
            {
                errors.Add($"Ключът в Qr:Keys с Id {key.Id} трябва да е Base64 и поне {QrTokenFormat.MinimumKeyBytes} байта.");
            }
            else if (jwtKey is not null && bytes.AsSpan().SequenceEqual(jwtKey))
            {
                // ADR-004: ключът за JWT и ключът за QR токените са различни, за да не може един да се ползва като другия.
                errors.Add("Ключът за QR токените трябва да е различен от Jwt:SigningKey.");
            }
        }

        if (options.TokenLifetime < QrOptions.MinTokenLifetime || options.TokenLifetime > QrOptions.MaxTokenLifetime)
        {
            errors.Add("Qr:TokenLifetime трябва да е между 30 и 120 секунди (ADR-006).");
        }

        if (options.RefreshBeforeExpiry <= TimeSpan.Zero || options.RefreshBeforeExpiry >= options.TokenLifetime)
        {
            errors.Add("Qr:RefreshBeforeExpiry трябва да е положително и по-малко от Qr:TokenLifetime.");
        }

        if (options.ScanSessionLifetime <= TimeSpan.Zero || options.ScanSessionLifetime > TimeSpan.FromMinutes(15))
        {
            errors.Add("Qr:ScanSessionLifetime трябва да е между 1 секунда и 15 минути.");
        }

        if (options.NonceCleanupInterval <= TimeSpan.Zero)
        {
            errors.Add("Qr:NonceCleanupInterval трябва да е положително.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static byte[]? TryDecode(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
