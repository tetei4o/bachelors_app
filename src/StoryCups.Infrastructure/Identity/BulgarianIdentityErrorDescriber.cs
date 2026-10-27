using Microsoft.AspNetCore.Identity;

namespace StoryCups.Infrastructure.Identity;

/// <summary>
/// Съобщенията на ASP.NET Core Identity на български (A15).
/// При дублиран имейл не казваме, че акаунтът съществува; регистрацията обработва този случай отделно.
/// </summary>
public sealed class BulgarianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Describe(nameof(DefaultError), "Възникна грешка. Опитайте отново.");

    public override IdentityError ConcurrencyFailure() => Describe(nameof(ConcurrencyFailure), "Данните са променени от друго място. Презаредете и опитайте отново.");

    public override IdentityError PasswordMismatch() => Describe(nameof(PasswordMismatch), "Грешна парола.");

    public override IdentityError InvalidToken() => Describe(nameof(InvalidToken), "Връзката е невалидна или е изтекла.");

    public override IdentityError InvalidEmail(string? email) => Describe(nameof(InvalidEmail), "Невалиден имейл.");

    public override IdentityError InvalidUserName(string? userName) => Describe(nameof(InvalidUserName), "Невалиден имейл.");

    public override IdentityError DuplicateEmail(string email) => Describe(nameof(DuplicateEmail), "Регистрацията не може да бъде завършена с този имейл.");

    public override IdentityError DuplicateUserName(string userName) => Describe(nameof(DuplicateUserName), "Регистрацията не може да бъде завършена с този имейл.");

    public override IdentityError PasswordTooShort(int length) => Describe(nameof(PasswordTooShort), $"Паролата трябва да е поне {length} символа.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Describe(nameof(PasswordRequiresUniqueChars), $"Паролата трябва да съдържа поне {uniqueChars} различни символа.");

    public override IdentityError PasswordRequiresNonAlphanumeric() => Describe(nameof(PasswordRequiresNonAlphanumeric), "Паролата трябва да съдържа поне един специален символ.");

    public override IdentityError PasswordRequiresDigit() => Describe(nameof(PasswordRequiresDigit), "Паролата трябва да съдържа поне една цифра.");

    public override IdentityError PasswordRequiresLower() => Describe(nameof(PasswordRequiresLower), "Паролата трябва да съдържа поне една малка буква.");

    public override IdentityError PasswordRequiresUpper() => Describe(nameof(PasswordRequiresUpper), "Паролата трябва да съдържа поне една главна буква.");

    public override IdentityError UserAlreadyHasPassword() => Describe(nameof(UserAlreadyHasPassword), "Потребителят вече има парола.");

    public override IdentityError UserLockoutNotEnabled() => Describe(nameof(UserLockoutNotEnabled), "Заключването не е разрешено за този потребител.");

    public override IdentityError UserAlreadyInRole(string role) => Describe(nameof(UserAlreadyInRole), $"Потребителят вече има роля {role}.");

    public override IdentityError UserNotInRole(string role) => Describe(nameof(UserNotInRole), $"Потребителят няма роля {role}.");

    public override IdentityError RecoveryCodeRedemptionFailed() => Describe(nameof(RecoveryCodeRedemptionFailed), "Невалиден код за възстановяване.");

    private static IdentityError Describe(string code, string description) => new() { Code = code, Description = description };
}
