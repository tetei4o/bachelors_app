using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using StoryCups.Contracts;
using StoryCups.Contracts.Auth;

namespace StoryCups.Mobile.Auth;

/// <summary>
/// Сесията на приложението (ADR-004). Access токенът е само в паметта, refresh токенът е в SecureStorage
/// (Android Keystore). Опресняването е сериализирано: две едновременни ротации на един и същ refresh токен
/// сървърът приема за кражба и отнема цялата верига.
/// </summary>
public sealed class AuthSession(
    HttpClient http,
    ApiSettings settings,
    ISecureStorage secureStorage,
    TimeProvider timeProvider,
    ILogger<AuthSession> logger)
{
    private const string RefreshTokenKey = "storycups.refresh_token";

    /// <summary>
    /// Опресняваме малко преди изтичане, за да не тръгне заявка с току-що изтекъл токен.
    /// </summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private readonly SemaphoreSlim gate = new(1, 1);
    private string? accessToken;
    private DateTimeOffset accessTokenExpiresAt;

    public ClaimsPrincipal User { get; private set; } = Anonymous;

    public bool IsSignedIn => User.Identity?.IsAuthenticated == true;

    public event Action? Changed;

    /// <summary>
    /// Вход с имейл и парола. Връща null при успех или съобщение за грешка.
    /// Липсата на връзка хвърля HttpRequestException (ADR-007: само онлайн).
    /// </summary>
    public async Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            settings.Resolve(ApiRoutes.Auth.Login), new LoginRequest { Email = email, Password = password }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return await ProblemReader.ReadMessageAsync(response, cancellationToken);
        }

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Празен отговор от сървъра.");
        await ApplyAsync(tokens);
        return null;
    }

    /// <summary>
    /// При старт: ако има запазен refresh токен, взима нов access токен.
    /// </summary>
    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSignedIn)
        {
            await GetAccessTokenAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Валиден access токен или null, ако потребителят не е влязъл.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (IsFresh())
        {
            return accessToken;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
            {
                return accessToken;
            }

            var refreshToken = await secureStorage.GetAsync(RefreshTokenKey);
            if (refreshToken is null)
            {
                return null;
            }

            using var response = await http.PostAsJsonAsync(
                settings.Resolve(ApiRoutes.Auth.Refresh), new RefreshRequest { RefreshToken = refreshToken }, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                logger.LogInformation("Refresh токенът е отказан; нужен е нов вход.");
                await ClearAsync();
                return null;
            }

            response.EnsureSuccessStatusCode();
            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Празен отговор от сървъра.");
            await ApplyAsync(tokens);
            return accessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await secureStorage.GetAsync(RefreshTokenKey);
        if (refreshToken is not null && settings.BaseUri is not null)
        {
            try
            {
                using var _ = await http.PostAsJsonAsync(
                    settings.Resolve(ApiRoutes.Auth.Logout), new RefreshRequest { RefreshToken = refreshToken }, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                // Без връзка токенът изтича сам; локално излизаме въпреки това.
                logger.LogWarning(ex, "Изходът не стигна до сървъра.");
            }
        }

        await ClearAsync();
    }

    /// <summary>
    /// Сървърът е отказал access токена (например след нова парола): локален изход.
    /// </summary>
    public Task SignOutLocallyAsync() => ClearAsync();

    private bool IsFresh() => accessToken is not null && timeProvider.GetUtcNow() < accessTokenExpiresAt - RefreshMargin;

    private async Task ApplyAsync(TokenResponse tokens)
    {
        await secureStorage.SetAsync(RefreshTokenKey, tokens.RefreshToken);
        accessToken = tokens.AccessToken;
        accessTokenExpiresAt = tokens.AccessTokenExpiresAt;
        User = AccessTokenReader.ToPrincipal(tokens.AccessToken);
        Changed?.Invoke();
    }

    private Task ClearAsync()
    {
        secureStorage.Remove(RefreshTokenKey);
        accessToken = null;
        User = Anonymous;
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
