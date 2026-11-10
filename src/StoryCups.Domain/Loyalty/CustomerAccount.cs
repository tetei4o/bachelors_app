namespace StoryCups.Domain.Loyalty;

/// <summary>
/// Клиентска сметка в програмата за лоялност. Не пази баланс: той се извежда от записите в ledger-а.
/// Сметката е котвата за optimistic concurrency: всяко действие увеличава <see cref="ActionCount"/> в същата
/// транзакция, така че две едновременни действия върху един клиент не могат да минат и двете (ADR-015).
/// </summary>
public sealed class CustomerAccount
{
    private CustomerAccount()
    {
    }

    /// <summary>
    /// Собствен ключ, различен от ключа на потребителя: при изтриване на акаунта връзката към човека се прекъсва,
    /// а записите в ledger-а остават непроменени.
    /// </summary>
    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    /// <summary>Брой действия по сметката; номерът на последното действие.</summary>
    public int ActionCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static CustomerAccount Open(Guid userId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        UserId = userId,
        CreatedAt = now,
    };

    /// <summary>
    /// Поредният номер на следващото действие. Промяната на реда на сметката задейства проверката на версията му.
    /// </summary>
    public int NextSequence() => ++ActionCount;
}
