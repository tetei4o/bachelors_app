namespace StoryCups.Contracts.Auth;

/// <summary>
/// Кодове на грешки, които API-то връща в полето "code" на ProblemDetails.
/// </summary>
public static class AuthErrorCodes
{
    public const string InvalidCredentials = "invalid_credentials";
    public const string EmailNotConfirmed = "email_not_confirmed";
    public const string LockedOut = "locked_out";
    public const string InvalidRefreshToken = "invalid_refresh_token";
    public const string RegistrationFailed = "registration_failed";
    public const string InvalidLink = "invalid_link";
    public const string PasswordResetFailed = "password_reset_failed";
    public const string Validation = "validation";
}
