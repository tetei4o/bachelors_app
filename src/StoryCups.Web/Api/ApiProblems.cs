using Microsoft.AspNetCore.Http.HttpResults;
using StoryCups.Application.Common;
using StoryCups.Contracts.Auth;
using StoryCups.Contracts.Loyalty;

namespace StoryCups.Web.Api;

/// <summary>
/// Превръща грешка от use case в ProblemDetails с машинен код в полето "code".
/// </summary>
internal static class ApiProblems
{
    public static ProblemHttpResult From(Error error) => TypedResults.Problem(
        statusCode: StatusFor(error.Code),
        title: error.Message,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    private static int StatusFor(string code) => code switch
    {
        AuthErrorCodes.InvalidCredentials => StatusCodes.Status401Unauthorized,
        AuthErrorCodes.InvalidRefreshToken => StatusCodes.Status401Unauthorized,
        AuthErrorCodes.EmailNotConfirmed => StatusCodes.Status403Forbidden,
        AuthErrorCodes.LockedOut => StatusCodes.Status403Forbidden,

        // QR токен и сканиране: изтеклото вече го няма (410), а използваният код и неактивният клиент
        // са конфликт със състоянието на сървъра (409).
        LoyaltyErrorCodes.QrExpired => StatusCodes.Status410Gone,
        LoyaltyErrorCodes.QrAlreadyUsed => StatusCodes.Status409Conflict,
        LoyaltyErrorCodes.CustomerInactive => StatusCodes.Status409Conflict,
        LoyaltyErrorCodes.SelfScan => StatusCodes.Status409Conflict,

        // Действия с печати. Липсващото го няма (404, 410); заявка, която е разбрана, но нарушава правило
        // за съдържанието си, е 422; отказ заради текущото състояние на сметката е 409.
        LoyaltyErrorCodes.ScanSessionNotFound => StatusCodes.Status404NotFound,
        LoyaltyErrorCodes.ActionNotFound => StatusCodes.Status404NotFound,
        LoyaltyErrorCodes.ScanSessionExpired => StatusCodes.Status410Gone,
        LoyaltyErrorCodes.IdempotencyKeyReused => StatusCodes.Status422UnprocessableEntity,
        LoyaltyErrorCodes.UnknownProduct => StatusCodes.Status422UnprocessableEntity,
        LoyaltyErrorCodes.NothingToRecord => StatusCodes.Status422UnprocessableEntity,
        LoyaltyErrorCodes.ProductNotEligible => StatusCodes.Status422UnprocessableEntity,
        LoyaltyErrorCodes.NoRewardAvailable => StatusCodes.Status409Conflict,
        LoyaltyErrorCodes.ConcurrentUpdate => StatusCodes.Status409Conflict,
        LoyaltyErrorCodes.NotReversible => StatusCodes.Status409Conflict,
        LoyaltyErrorCodes.AlreadyReversed => StatusCodes.Status409Conflict,
        LoyaltyErrorCodes.UndoWindowExpired => StatusCodes.Status409Conflict,
        LoyaltyErrorCodes.NotLastAction => StatusCodes.Status409Conflict,
        LoyaltyErrorCodes.ReversalForbidden => StatusCodes.Status403Forbidden,

        _ => StatusCodes.Status400BadRequest,
    };
}
