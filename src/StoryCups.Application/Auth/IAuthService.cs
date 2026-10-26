using StoryCups.Application.Common;
using StoryCups.Contracts.Auth;

namespace StoryCups.Application.Auth;

/// <summary>
/// Акаунти и автентикация (ADR-004). Ползва се от страниците на сайта и от API-то за мобилното приложение,
/// за да имат двата входа едни и същи правила.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Създава клиентски акаунт и изпраща имейл за потвърждение.
    /// При вече използван имейл връща същия успешен отговор и пише на собственика на адреса.
    /// </summary>
    Task<Result<RegistrationAccepted>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Потвърждава имейла по връзката от писмото.
    /// </summary>
    Task<Result> ConfirmEmailAsync(Guid userId, string encodedCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Изпраща отново писмото за потвърждение. Не издава дали имейлът има акаунт.
    /// </summary>
    Task ResendConfirmationAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверява имейл и парола. Грешните опити се броят за временно заключване.
    /// Връща Id на потребителя; сайтът го ползва, за да издаде бисквитка.
    /// </summary>
    Task<Result<Guid>> CheckCredentialsAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Вход от мобилното приложение: access токен и нова верига refresh токени.
    /// </summary>
    Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<Result<TokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отнема refresh токена. Непознат или вече отнет токен не е грешка.
    /// </summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Изпраща връзка за нова парола, ако имейлът има потвърден акаунт. Отговорът не издава кое от двете е вярно.
    /// </summary>
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Задава нова парола по кода от писмото и отнема всички refresh токени на потребителя.
    /// </summary>
    Task<Result> ResetPasswordAsync(string email, string encodedCode, string newPassword, CancellationToken cancellationToken = default);
}

/// <summary>
/// Регистрацията е приета; акаунтът чака потвърждение на имейла.
/// </summary>
public sealed record RegistrationAccepted(string Email);
