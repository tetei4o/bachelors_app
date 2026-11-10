using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StoryCups.Contracts;
using StoryCups.Contracts.Auth;
using StoryCups.Contracts.Loyalty;

namespace StoryCups.IntegrationTests.Infrastructure;

/// <summary>
/// Помощни заявки към API-то, както би ги пратило мобилното приложение.
/// </summary>
public static class ApiClient
{
    public const string Password = "дълга тестова фраза";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync(ApiRoutes.Auth.Register, new RegisterRequest
        {
            Email = email,
            Password = password,
            DisplayName = "Тест Клиент",
            AcceptPrivacyPolicy = true,
        });

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync(ApiRoutes.Auth.Login, new LoginRequest { Email = email, Password = password });

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync(ApiRoutes.Auth.Refresh, new RefreshRequest { RefreshToken = refreshToken });

    public static Task<HttpResponseMessage> GetMeAsync(this HttpClient client, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Profile.Me);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client.SendAsync(request);
    }

    /// <summary>
    /// Регистрира акаунт и отваря връзката от писмото, както би направил потребителят.
    /// </summary>
    public static async Task RegisterAndConfirmAsync(this HttpClient client, CapturingEmailSender emails, string email)
    {
        var registered = await client.RegisterAsync(email);
        Assert.Equal(HttpStatusCode.Accepted, registered.StatusCode);

        var link = new Uri(emails.Last(email, EmailKind.Confirmation).Link);
        var confirmPage = await client.GetStringAsync(link.PathAndQuery);
        Assert.Contains("Имейлът е потвърден", confirmPage);
    }

    public static async Task<TokenResponse> LoginOkAsync(this HttpClient client, string email, string password = Password)
    {
        var response = await client.LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    /// <summary>
    /// Заявка с Bearer токен и по желание JSON тяло и заглавие Idempotency-Key.
    /// </summary>
    public static Task<HttpResponseMessage> SendAsync(
        this HttpClient client, HttpMethod method, string path, string? accessToken, object? body = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> IssueQrTokenAsync(this HttpClient client, string? accessToken) =>
        client.SendAsync(HttpMethod.Post, ApiRoutes.Qr.Tokens, accessToken);

    public static async Task<QrTokenResponse> IssueQrTokenOkAsync(this HttpClient client, TestUser customer)
    {
        var response = await client.IssueQrTokenAsync(customer.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<QrTokenResponse>())!;
    }

    public static Task<HttpResponseMessage> ScanAsync(this HttpClient client, string? accessToken, string token) =>
        client.SendAsync(HttpMethod.Post, ApiRoutes.Qr.Scans, accessToken, new ScanRequest { Token = token });

    /// <summary>
    /// Клиентът показва нов код и баристата го сканира успешно.
    /// </summary>
    public static async Task<ScanSessionDto> ScanOkAsync(this HttpClient client, TestUser barista, TestUser customer)
    {
        var token = await client.IssueQrTokenOkAsync(customer);
        var response = await client.ScanAsync(barista.AccessToken, token.Token);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ScanSessionDto>())!;
    }

    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return problem.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
