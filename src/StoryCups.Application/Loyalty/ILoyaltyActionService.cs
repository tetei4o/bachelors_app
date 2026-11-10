using StoryCups.Application.Common;
using StoryCups.Contracts.Loyalty;

namespace StoryCups.Application.Loyalty;

/// <summary>
/// Резултат от действие и дали е отговор на повторена заявка (същият Idempotency-Key, същото съдържание).
/// </summary>
public sealed record LoyaltyActionOutcome(LoyaltyActionResultDto Result, bool Replayed);

/// <summary>
/// Действията с печати и награди (ADR-008, ADR-015). Всяко действие е една транзакция: проверка на правилата,
/// запис на действието и на редовете му в ledger-а. Повторена заявка със същия ключ не се изпълнява втори път.
/// </summary>
public interface ILoyaltyActionService
{
    /// <summary>
    /// Покупка или използване на награда в сесия на сканиране, отворена от същия служител.
    /// </summary>
    Task<Result<LoyaltyActionOutcome>> SubmitAsync(
        Guid actorId,
        Guid scanSessionId,
        LoyaltyActionRequest request,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмяна на последното действие по сметката с обратни записи. Без сесия на сканиране.
    /// </summary>
    Task<Result<LoyaltyActionOutcome>> ReverseAsync(
        Guid actorId,
        bool actorIsAdmin,
        Guid actionId,
        string reason,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);
}
