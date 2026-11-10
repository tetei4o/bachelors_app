namespace StoryCups.Domain.Qr;

/// <summary>
/// Nonce на вече сканиран QR токен (ADR-005). Стойността е първичен ключ: вторият опит за запис на същия nonce
/// се отхвърля от базата и така един код не може да се използва два пъти, дори при едновременни заявки.
/// Редът се изтрива, след като токенът е изтекъл, защото тогава го спира проверката на срока.
/// </summary>
public sealed class UsedQrNonce
{
    private UsedQrNonce()
    {
    }

    public byte[] Nonce { get; private set; } = [];

    public DateTimeOffset ExpiresAt { get; private set; }

    public static UsedQrNonce From(QrTokenPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Nonce.Length != QrTokenFormat.NonceLength)
        {
            throw new ArgumentException($"Nonce трябва да е {QrTokenFormat.NonceLength} байта.", nameof(payload));
        }

        return new UsedQrNonce
        {
            Nonce = (byte[])payload.Nonce.Clone(),
            ExpiresAt = payload.ExpiresAt,
        };
    }
}
