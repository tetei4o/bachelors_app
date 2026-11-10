using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StoryCups.Application.Common;
using StoryCups.Application.Loyalty;
using StoryCups.Contracts.Loyalty;
using StoryCups.Domain.Loyalty;
using StoryCups.Infrastructure.Persistence;

namespace StoryCups.Infrastructure.Loyalty;

/// <summary>
/// Действията с печати и награди (ADR-008, ADR-015).
/// <para>
/// Идемпотентност: преди всичко друго се търси действие със същия ключ на същия служител. Ако има, връща се
/// неговият резултат и нищо не се записва; уникалният индекс (ActorId, IdempotencyKey) пази и при две едновременни
/// заявки със същия ключ.
/// </para>
/// <para>
/// Едновременни действия: редът на сметката се чете проследен и версията му (xmin) се проверява при записа.
/// Действието увеличава брояча на сметката, затова всяко друго действие, записано междувременно, проваля
/// проверката. Тогава опитът се повтаря от начало с нов баланс. SaveChanges е една транзакция: сметката,
/// действието и записите в ledger-а се записват заедно или изобщо не се записват.
/// </para>
/// </summary>
public sealed class LoyaltyActionService(
    AppDbContext db,
    TimeProvider timeProvider,
    ILogger<LoyaltyActionService> logger) : ILoyaltyActionService
{
    /// <summary>Колко пъти се повтаря действие, изпреварено от друго по същата сметка.</summary>
    internal const int MaxAttempts = 5;

    public Task<Result<LoyaltyActionOutcome>> SubmitAsync(
        Guid actorId,
        Guid scanSessionId,
        LoyaltyActionRequest request,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var isPurchase = request.Type == LoyaltyActionTypes.Purchase;
        var requestHash = isPurchase
            ? ActionFingerprint.ForPurchase(scanSessionId, request.Items!.Select(item => (item.ProductId, item.Quantity)))
            : ActionFingerprint.ForRedemption(scanSessionId, request.ProductId!.Value);

        return ExecuteAsync(
            actorId,
            idempotencyKey,
            requestHash,
            async (settings, now) =>
            {
                var session = await db.ScanSessions.AsNoTracking()
                    .SingleOrDefaultAsync(s => s.Id == scanSessionId, cancellationToken);
                if (session is null || !session.BelongsTo(actorId))
                {
                    return Rejected(LoyaltyErrorCodes.ScanSessionNotFound, "Сесията не е намерена. Сканирайте кода на клиента отново.");
                }

                if (session.IsExpired(now))
                {
                    return Rejected(LoyaltyErrorCodes.ScanSessionExpired, "Сесията е изтекла. Сканирайте новия код на клиента.");
                }

                var productIds = isPurchase
                    ? request.Items!.Select(item => item.ProductId).Distinct().ToList()
                    : [request.ProductId!.Value];
                var products = await db.Products.AsNoTracking()
                    .Where(product => product.IsActive && productIds.Contains(product.Id))
                    .ToDictionaryAsync(product => product.Id, cancellationToken);
                if (products.Count != productIds.Count)
                {
                    return Rejected(LoyaltyErrorCodes.UnknownProduct, "Избран е продукт, който не е в менюто.");
                }

                // Сметката се чете проследена преди баланса: версията ѝ от този момент пази цялото действие.
                var account = await db.CustomerAccounts.SingleAsync(a => a.Id == session.AccountId, cancellationToken);
                var balance = await db.GetBalanceAsync(account.Id, cancellationToken);

                var rule = isPurchase
                    ? LoyaltyRules.Purchase(
                        balance,
                        [.. request.Items!.Select(item => new PurchaseLine(item.ProductId, products[item.ProductId].EarnsStamp, item.Quantity))],
                        settings)
                    : LoyaltyRules.RedeemReward(balance, request.ProductId!.Value, products[request.ProductId.Value].EarnsStamp);
                if (!rule.IsAllowed)
                {
                    return Denied(rule.Denial!.Value);
                }

                return Result<LoyaltyAction>.Success(LoyaltyAction.Record(
                    account,
                    isPurchase ? LoyaltyActionType.Purchase : LoyaltyActionType.RewardRedemption,
                    actorId,
                    idempotencyKey,
                    requestHash,
                    rule.Outcome!,
                    now,
                    scanSessionId: session.Id));
            },
            cancellationToken);
    }

    public Task<Result<LoyaltyActionOutcome>> ReverseAsync(
        Guid actorId,
        bool actorIsAdmin,
        Guid actionId,
        string reason,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        reason = reason.Trim();
        var requestHash = ActionFingerprint.ForReversal(actionId, reason);

        return ExecuteAsync(
            actorId,
            idempotencyKey,
            requestHash,
            async (settings, now) =>
            {
                var target = await db.LoyaltyActions.AsNoTracking()
                    .Include(action => action.Entries)
                    .SingleOrDefaultAsync(action => action.Id == actionId, cancellationToken);
                if (target is null)
                {
                    return Rejected(LoyaltyErrorCodes.ActionNotFound, "Действието не е намерено.");
                }

                var account = await db.CustomerAccounts.SingleAsync(a => a.Id == target.AccountId, cancellationToken);
                var balance = await db.GetBalanceAsync(account.Id, cancellationToken);
                var alreadyReversed = await db.LoyaltyActions
                    .AnyAsync(action => action.ReversesActionId == actionId, cancellationToken);

                // Последното действие, което още важи: не е отмяна и не е отменено.
                var lastEffectiveActionId = await db.LoyaltyActions
                    .Where(action => action.AccountId == account.Id
                        && action.Type != LoyaltyActionType.Reversal
                        && !db.LoyaltyActions.Any(reversal => reversal.ReversesActionId == action.Id))
                    .OrderByDescending(action => action.Sequence)
                    .Select(action => (Guid?)action.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                var rule = LoyaltyRules.Reverse(
                    balance,
                    target,
                    new ReversalContext(actorId, actorIsAdmin, alreadyReversed, lastEffectiveActionId, now),
                    settings);
                if (!rule.IsAllowed)
                {
                    return Denied(rule.Denial!.Value);
                }

                logger.LogInformation(
                    "Отмяна на действие {ActionId} от {ActorId} (администратор: {IsAdmin}).", actionId, actorId, actorIsAdmin);
                return Result<LoyaltyAction>.Success(LoyaltyAction.Record(
                    account,
                    LoyaltyActionType.Reversal,
                    actorId,
                    idempotencyKey,
                    requestHash,
                    rule.Outcome!,
                    now,
                    reversesActionId: target.Id,
                    reason: reason));
            },
            cancellationToken);
    }

    internal static LoyaltyActionResultDto ToResult(LoyaltyAction action, LoyaltySettings settings) => new(
        action.Id,
        TypeName(action.Type),
        action.CreatedAt,
        StampsAdded: action.Entries.Where(e => e.Kind == LedgerEntryKind.Purchase).Sum(e => e.StampDelta),
        RewardsEarned: action.Entries.Where(e => e.Kind == LedgerEntryKind.RewardEarned).Sum(e => e.RewardDelta),
        RewardsRedeemed: -action.Entries.Where(e => e.Kind == LedgerEntryKind.RewardRedeemed).Sum(e => e.RewardDelta),
        new LoyaltyStatusDto(action.StampsAfter, action.RewardsAfter, settings.StampThreshold),
        UndoUntil: action.Type == LoyaltyActionType.Reversal ? null : action.CreatedAt + settings.UndoWindow,
        action.ReversesActionId);

    internal static string TypeName(LoyaltyActionType type) => type switch
    {
        LoyaltyActionType.Purchase => LoyaltyActionTypes.Purchase,
        LoyaltyActionType.RewardRedemption => LoyaltyActionTypes.RedeemReward,
        LoyaltyActionType.Reversal => LoyaltyActionTypes.Reversal,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>
    /// Общият път на всяко действие: търсене по идемпотентен ключ, подготовка, запис и повторение при
    /// изпреварване от друго действие по същата сметка.
    /// </summary>
    private async Task<Result<LoyaltyActionOutcome>> ExecuteAsync(
        Guid actorId,
        Guid idempotencyKey,
        string requestHash,
        Func<LoyaltySettings, DateTimeOffset, Task<Result<LoyaltyAction>>> prepare,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            // Всеки опит започва на чисто: нищо от провален опит не остава проследено.
            db.ChangeTracker.Clear();
            var settings = await db.GetLoyaltySettingsAsync(cancellationToken);

            var existing = await db.LoyaltyActions.AsNoTracking()
                .Include(action => action.Entries)
                .SingleOrDefaultAsync(
                    action => action.ActorId == actorId && action.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return existing.RequestHash == requestHash
                    ? Result<LoyaltyActionOutcome>.Success(new LoyaltyActionOutcome(ToResult(existing, settings), Replayed: true))
                    : Result<LoyaltyActionOutcome>.Failure(
                        LoyaltyErrorCodes.IdempotencyKeyReused,
                        "Този Idempotency-Key вече е използван за друго действие.");
            }

            var prepared = await prepare(settings, Now());
            if (!prepared.IsSuccess)
            {
                return Result<LoyaltyActionOutcome>.Failure(prepared.Error!.Code, prepared.Error.Message);
            }

            var action = prepared.Value!;
            db.LoyaltyActions.Add(action);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return Result<LoyaltyActionOutcome>.Success(new LoyaltyActionOutcome(ToResult(action, settings), Replayed: false));
            }
            catch (DbUpdateConcurrencyException)
            {
                // Друго действие по същата сметка е записано след прочитането ѝ: балансът вече е друг.
                logger.LogInformation(
                    "Едновременно действие по сметка {AccountId}, опит {Attempt} от {MaxAttempts}.",
                    action.AccountId, attempt, MaxAttempts);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueViolation())
            {
                // Същият ключ, пореден номер или отмяна са записани от друга заявка. Следващият опит ще намери
                // записаното действие по ключа или ще получи отказ от правилата.
                logger.LogInformation(
                    "Сблъсък на уникален индекс при действие по сметка {AccountId}, опит {Attempt} от {MaxAttempts}.",
                    action.AccountId, attempt, MaxAttempts);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(5, 20) * attempt), cancellationToken);
        }

        logger.LogWarning("Действие от {ActorId} не е записано след {MaxAttempts} опита.", actorId, MaxAttempts);
        return Result<LoyaltyActionOutcome>.Failure(
            LoyaltyErrorCodes.ConcurrentUpdate, "Сметката се променя в момента. Опитайте отново.");
    }

    /// <summary>
    /// Времето до микросекунда, колкото пази PostgreSQL: първият отговор и отговорът на повторена заявка съвпадат.
    /// </summary>
    private DateTimeOffset Now()
    {
        var now = timeProvider.GetUtcNow();
        return now.AddTicks(-(now.Ticks % 10));
    }

    private static Result<LoyaltyAction> Rejected(string code, string message) =>
        Result<LoyaltyAction>.Failure(code, message);

    private static Result<LoyaltyAction> Denied(RuleDenial denial) => denial switch
    {
        RuleDenial.NothingToRecord => Rejected(
            LoyaltyErrorCodes.NothingToRecord, "Избраните продукти не носят печат."),
        RuleDenial.NoRewardAvailable => Rejected(
            LoyaltyErrorCodes.NoRewardAvailable, "Клиентът няма налична награда."),
        RuleDenial.ProductNotEligible => Rejected(
            LoyaltyErrorCodes.ProductNotEligible, "Наградата важи само за напитки, които носят печат."),
        RuleDenial.NotReversible => Rejected(
            LoyaltyErrorCodes.NotReversible, "Отмяна не може да се отмени."),
        RuleDenial.AlreadyReversed => Rejected(
            LoyaltyErrorCodes.AlreadyReversed, "Действието вече е отменено."),
        RuleDenial.NotActor => Rejected(
            LoyaltyErrorCodes.ReversalForbidden, "Действието се отменя от баристата, който го е направил, или от администратор."),
        RuleDenial.UndoWindowExpired => Rejected(
            LoyaltyErrorCodes.UndoWindowExpired, "Срокът за отмяна е изтекъл."),
        RuleDenial.NotLastAction => Rejected(
            LoyaltyErrorCodes.NotLastAction, "Отменя се само последното действие по картата на клиента."),
        _ => throw new ArgumentOutOfRangeException(nameof(denial), denial, null),
    };
}
