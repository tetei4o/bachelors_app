using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoryCups.Application.Auth;
using StoryCups.Application.Common;
using StoryCups.Contracts.Auth;
using StoryCups.Domain.Identity;
using StoryCups.Infrastructure.Email;
using StoryCups.Infrastructure.Identity;
using StoryCups.Infrastructure.Persistence;

namespace StoryCups.Infrastructure.Auth;

/// <summary>
/// Акаунти и автентикация (ADR-004). Общи правила за сайта и за мобилното приложение.
/// </summary>
public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AppDbContext db,
    AccessTokenIssuer accessTokenIssuer,
    IEmailSender<ApplicationUser> emailSender,
    IAccountNotifier accountNotifier,
    AccountLinks links,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidCredentialsMessage = "Грешен имейл или парола.";
    private const string InvalidRefreshMessage = "Сесията е изтекла. Влезте отново.";
    private const string InvalidLinkMessage = "Връзката е невалидна или е изтекла.";

    public async Task<Result<RegistrationAccepted>> RegisterAsync(
        RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();

        // Съществуващ имейл: същият отговор като при успех и писмо до собственика на адреса.
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            await accountNotifier.SendAccountExistsAsync(email, HtmlEncoder.Default.Encode(links.Login()));
            logger.LogInformation("Повторна регистрация с вече използван имейл.");
            return Result<RegistrationAccepted>.Success(new RegistrationAccepted(email));
        }

        var now = timeProvider.GetUtcNow();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PrivacyConsentAt = now,
            CreatedAt = now,
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return Result<RegistrationAccepted>.Failure(
                AuthErrorCodes.RegistrationFailed,
                string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        var roleAdded = await userManager.AddToRoleAsync(user, AppRoles.Customer);
        if (!roleAdded.Succeeded)
        {
            throw new InvalidOperationException("Ролята Customer липсва. Приложени ли са миграциите?");
        }

        await SendConfirmationAsync(user);
        return Result<RegistrationAccepted>.Success(new RegistrationAccepted(email));
    }

    public async Task<Result> ConfirmEmailAsync(Guid userId, string encodedCode, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        var code = TryDecode(encodedCode);
        if (user is null || code is null)
        {
            return Result.Failure(AuthErrorCodes.InvalidLink, InvalidLinkMessage);
        }

        var confirmed = await userManager.ConfirmEmailAsync(user, code);
        return confirmed.Succeeded
            ? Result.Success()
            : Result.Failure(AuthErrorCodes.InvalidLink, InvalidLinkMessage);
    }

    public async Task ResendConfirmationAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || await userManager.IsEmailConfirmedAsync(user))
        {
            return;
        }

        await SendConfirmationAsync(user);
    }

    public async Task<Result<Guid>> CheckCredentialsAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Result<Guid>.Failure(AuthErrorCodes.InvalidCredentials, InvalidCredentialsMessage);
        }

        var signIn = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (signIn.Succeeded)
        {
            return Result<Guid>.Success(user.Id);
        }

        if (signIn.IsNotAllowed)
        {
            // SignInManager отказва непотвърден акаунт, преди да провери паролата и заключването.
            // Затова ги проверяваме тук: „непотвърден имейл“ казваме само при вярна парола,
            // а грешната парола се брои за заключване както при потвърден акаунт.
            if (await userManager.IsLockedOutAsync(user))
            {
                return LockedOut();
            }

            if (await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Result<Guid>.Failure(AuthErrorCodes.EmailNotConfirmed, "Потвърдете имейла си чрез връзката, която ви изпратихме.");
            }

            await userManager.AccessFailedAsync(user);
            return await userManager.IsLockedOutAsync(user)
                ? LockedOut()
                : Result<Guid>.Failure(AuthErrorCodes.InvalidCredentials, InvalidCredentialsMessage);
        }

        return signIn.IsLockedOut
            ? LockedOut()
            : Result<Guid>.Failure(AuthErrorCodes.InvalidCredentials, InvalidCredentialsMessage);

        static Result<Guid> LockedOut() => Result<Guid>.Failure(
            AuthErrorCodes.LockedOut, "Акаунтът е временно заключен след няколко грешни опита. Опитайте по-късно.");
    }

    public async Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var check = await CheckCredentialsAsync(request, cancellationToken);
        if (!check.IsSuccess)
        {
            return Result<TokenResponse>.Failure(check.Error!.Code, check.Error.Message);
        }

        var user = await userManager.FindByIdAsync(check.Value.ToString())
            ?? throw new InvalidOperationException("Потребителят изчезна между проверката и издаването на токен.");
        var tokens = await IssueTokensAsync(user, familyId: Guid.CreateVersion7(), parent: null, cancellationToken);
        return Result<TokenResponse>.Success(tokens);
    }

    public async Task<Result<TokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<TokenResponse>.Failure(AuthErrorCodes.InvalidRefreshToken, InvalidRefreshMessage);
        }

        var now = timeProvider.GetUtcNow();
        var hash = RefreshTokenGenerator.Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null)
        {
            return Result<TokenResponse>.Failure(AuthErrorCodes.InvalidRefreshToken, InvalidRefreshMessage);
        }

        if (stored.IsRevoked)
        {
            // Вече ротиран или отнет токен е използван отново: вероятна кражба. Отнемаме цялата верига (ADR-004).
            await RevokeAsync(t => t.FamilyId == stored.FamilyId, now, RefreshTokenRevocationReason.ReuseDetected, cancellationToken);
            logger.LogWarning("Повторно използван refresh токен; веригата {FamilyId} е отнета.", stored.FamilyId);
            return Result<TokenResponse>.Failure(AuthErrorCodes.InvalidRefreshToken, InvalidRefreshMessage);
        }

        if (stored.IsExpired(now))
        {
            return Result<TokenResponse>.Failure(AuthErrorCodes.InvalidRefreshToken, InvalidRefreshMessage);
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !await signInManager.CanSignInAsync(user) || await userManager.IsLockedOutAsync(user))
        {
            await RevokeAsync(t => t.Id == stored.Id, now, RefreshTokenRevocationReason.UserDeactivated, cancellationToken);
            return Result<TokenResponse>.Failure(AuthErrorCodes.InvalidRefreshToken, InvalidRefreshMessage);
        }

        try
        {
            var tokens = await IssueTokensAsync(user, stored.FamilyId, stored, cancellationToken);
            return Result<TokenResponse>.Success(tokens);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Друга заявка е ротирала същия токен междувременно; тази губи.
            return Result<TokenResponse>.Failure(AuthErrorCodes.InvalidRefreshToken, InvalidRefreshMessage);
        }
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = RefreshTokenGenerator.Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null)
        {
            return;
        }

        stored.Revoke(timeProvider.GetUtcNow(), RefreshTokenRevocationReason.Logout);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            return;
        }

        var code = Encode(await userManager.GeneratePasswordResetTokenAsync(user));
        await emailSender.SendPasswordResetLinkAsync(user, user.Email!, HtmlEncoder.Default.Encode(links.ResetPassword(code)));
    }

    public async Task<Result> ResetPasswordAsync(
        string email, string encodedCode, string newPassword, CancellationToken cancellationToken = default)
    {
        var code = TryDecode(encodedCode);
        if (code is null)
        {
            return Result.Failure(AuthErrorCodes.InvalidLink, InvalidLinkMessage);
        }

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            // Не издаваме дали имейлът съществува: отговорът е като при успех.
            return Result.Success();
        }

        var reset = await userManager.ResetPasswordAsync(user, code, newPassword);
        if (!reset.Succeeded)
        {
            return Result.Failure(AuthErrorCodes.PasswordResetFailed, string.Join(" ", reset.Errors.Select(e => e.Description)));
        }

        // Бисквитките отпадат чрез новия security stamp; мобилните сесии отнемаме изрично.
        await RevokeAsync(t => t.UserId == user.Id, timeProvider.GetUtcNow(), RefreshTokenRevocationReason.PasswordReset, cancellationToken);
        return Result.Success();
    }

    private async Task<TokenResponse> IssueTokensAsync(
        ApplicationUser user, Guid familyId, RefreshToken? parent, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpiresAt) = accessTokenIssuer.Issue(user, roles);

        var refreshToken = RefreshTokenGenerator.Generate();
        var stored = RefreshToken.Issue(
            user.Id, familyId, RefreshTokenGenerator.Hash(refreshToken), now, jwtOptions.Value.RefreshTokenLifetime);

        parent?.MarkReplaced(stored.Id, now);
        db.RefreshTokens.Add(stored);
        await db.SaveChangesAsync(cancellationToken);

        return new TokenResponse(accessToken, accessExpiresAt, refreshToken, stored.ExpiresAt);
    }

    /// <summary>
    /// Отнема наведнъж всички активни токени в обхвата. Използва един UPDATE в базата вместо зареждане и запис,
    /// за да не се провали от едновременна ротация на друг токен от същата верига.
    /// </summary>
    private Task RevokeAsync(
        System.Linq.Expressions.Expression<Func<RefreshToken, bool>> scope,
        DateTimeOffset now,
        RefreshTokenRevocationReason reason,
        CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(scope)
            .Where(t => t.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevocationReason, reason),
                cancellationToken);

    private async Task SendConfirmationAsync(ApplicationUser user)
    {
        var code = Encode(await userManager.GenerateEmailConfirmationTokenAsync(user));
        await emailSender.SendConfirmationLinkAsync(user, user.Email!, HtmlEncoder.Default.Encode(links.ConfirmEmail(user.Id, code)));
    }

    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string? TryDecode(string encodedCode)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedCode));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
