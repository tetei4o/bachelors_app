using Microsoft.AspNetCore.Http.HttpResults;
using StoryCups.Application.Common;
using StoryCups.Contracts.Auth;

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
        _ => StatusCodes.Status400BadRequest,
    };
}
