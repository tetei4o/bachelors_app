namespace StoryCups.Contracts.Auth;

/// <summary>
/// Правила за паролата (ADR-004). Паролата е единственият фактор при вход, затова минимумът е 15 символа
/// (NIST SP 800-63B-4, раздел 3.1.1.2). Правила за състав няма.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 15;

    public const int MaxLength = 128;

    public const string LengthError = "Паролата трябва да е между {2} и {1} символа.";
}
