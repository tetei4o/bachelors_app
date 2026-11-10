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

        _ => StatusCodes.Status400BadRequest,
    };
}
