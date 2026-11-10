namespace StoryCups.Domain.Qr;

/// <summary>
/// Сесия на сканиране (ADR-008): отваря се при успешно сканиране и обвързва баристата с клиентската сметка.
/// Действията (покупка, награда) се изпращат към сесията, затова едно сканиране покрива цялото посещение.
/// </summary>
public sealed class ScanSession
{
    private ScanSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid BaristaId { get; private set; }

    public Guid AccountId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public static ScanSession Open(Guid baristaId, Guid accountId, DateTimeOffset now, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Животът на сесията трябва да е положителен.");
        }

        return new ScanSession
        {
            Id = Guid.CreateVersion7(now),
            BaristaId = baristaId,
            AccountId = accountId,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        };
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Сесията важи само за баристата, който е сканирал кода.
    /// </summary>
    public bool BelongsTo(Guid baristaId) => BaristaId == baristaId;
}
