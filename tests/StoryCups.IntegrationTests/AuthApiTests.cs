using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StoryCups.Application.Auth;
using StoryCups.Contracts.Auth;
using StoryCups.Contracts.Profile;
using StoryCups.IntegrationTests.Infrastructure;

namespace StoryCups.IntegrationTests;

[Collection(AppCollection.Name)]
public sealed class AuthApiTests(StoryCupsAppFactory app)
{
    private readonly HttpClient client = app.CreateClient();

    [Fact]
    public async Task FullFlow_RegisterConfirmLoginMeRefresh()
    {
        var email = ApiClient.NewEmail();
        await client.RegisterAndConfirmAsync(app.Emails, email);

        var tokens = await client.LoginOkAsync(email);
        var me = await client.GetMeAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await me.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.Equal(email, profile!.Email);
        Assert.Equal(["Customer"], profile.Roles);

        var refreshed = await client.RefreshAsync(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var rotated = await refreshed.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotEqual(tokens.RefreshToken, rotated!.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetMeAsync(rotated.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task Refresh_ReusingRotatedToken_RevokesWholeFamily()
    {
        var email = ApiClient.NewEmail();
        await client.RegisterAndConfirmAsync(app.Emails, email);
        var first = await client.LoginOkAsync(email);
        var second = await (await client.RefreshAsync(first.RefreshToken)).Content.ReadFromJsonAsync<TokenResponse>();

        // Откраднатият стар токен се използва повторно.
        var reuse = await client.RefreshAsync(first.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(AuthErrorCodes.InvalidRefreshToken, await reuse.ErrorCodeAsync());

        // Веригата е отнета: и последният легитимен токен вече не работи.
        var afterReuse = await client.RefreshAsync(second!.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_ConcurrentRequestsWithSameToken_AtMostOneSucceeds()
    {
        var email = ApiClient.NewEmail();
        await client.RegisterAndConfirmAsync(app.Emails, email);
        var tokens = await client.LoginOkAsync(email);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.RefreshAsync(tokens.RefreshToken)));

        Assert.True(responses.Count(r => r.StatusCode == HttpStatusCode.OK) <= 1);
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized, $"{(int)r.StatusCode}: {r.Content.ReadAsStringAsync().Result}"));
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var email = ApiClient.NewEmail();
        await client.RegisterAndConfirmAsync(app.Emails, email);
        var tokens = await client.LoginOkAsync(email);

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(tokens.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Login_BeforeConfirmation_RevealsStatusOnlyWithCorrectPassword()
    {
        var email = ApiClient.NewEmail();
        Assert.Equal(HttpStatusCode.Accepted, (await client.RegisterAsync(email)).StatusCode);

        var wrongPassword = await client.LoginAsync(email, "грешна парола тук");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, await wrongPassword.ErrorCodeAsync());

        var rightPassword = await client.LoginAsync(email);
        Assert.Equal(HttpStatusCode.Forbidden, rightPassword.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailNotConfirmed, await rightPassword.ErrorCodeAsync());
    }

    [Fact]
    public async Task Login_UnknownEmail_LooksLikeWrongPassword()
    {
        var response = await client.LoginAsync(ApiClient.NewEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksAccount()
    {
        var email = ApiClient.NewEmail();
        await client.RegisterAndConfirmAsync(app.Emails, email);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(email, "грешна парола тук")).StatusCode);
        }

        var fifth = await client.LoginAsync(email, "грешна парола тук");
        Assert.Equal(AuthErrorCodes.LockedOut, await fifth.ErrorCodeAsync());

        // Дори вярната парола не помага, докато заключването е активно.
        var correct = await client.LoginAsync(email);
        Assert.Equal(AuthErrorCodes.LockedOut, await correct.ErrorCodeAsync());
    }

    [Fact]
    public async Task Register_ExistingEmail_ReturnsSameResponse_AndNotifiesOwner()
    {
        var email = ApiClient.NewEmail();
        var first = await client.RegisterAsync(email);
        var second = await client.RegisterAsync(email, "друга дълга парола");

        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Single(app.Emails.SentTo(email), e => e.Kind == EmailKind.AccountExists);
        Assert.Single(app.Emails.SentTo(email), e => e.Kind == EmailKind.Confirmation);
    }

    [Fact]
    public async Task Register_InvalidBody_Returns400WithFieldErrors()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = "не-е-имейл",
            Password = "кратка",
            DisplayName = "",
            AcceptPrivacyPolicy = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"Email\"", body);
        Assert.Contains("\"Password\"", body);
        Assert.Contains("\"DisplayName\"", body);
        Assert.Contains("\"AcceptPrivacyPolicy\"", body);
    }

    [Fact]
    public async Task Me_WithoutOrWithForgedToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetMeAsync(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetMeAsync("eyJhbGciOiJub25lIn0.e30.")).StatusCode);
    }

    [Fact]
    public async Task PasswordReset_RevokesAllRefreshTokens()
    {
        var email = ApiClient.NewEmail();
        await client.RegisterAndConfirmAsync(app.Emails, email);
        var tokens = await client.LoginOkAsync(email);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await auth.RequestPasswordResetAsync(email);
            var code = new Uri(app.Emails.Last(email, EmailKind.PasswordReset).Link).Query.Split("code=")[1];

            var reset = await auth.ResetPasswordAsync(email, code, "съвсем нова дълга парола");
            Assert.True(reset.IsSuccess, reset.Error?.Message);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(tokens.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(email)).StatusCode);
        await client.LoginOkAsync(email, "съвсем нова дълга парола");
    }

    [Fact]
    public async Task ConfirmEmail_WithTamperedCode_ShowsError()
    {
        var email = ApiClient.NewEmail();
        await client.RegisterAsync(email);
        var link = new Uri(app.Emails.Last(email, EmailKind.Confirmation).Link);

        var page = await client.GetStringAsync(link.AbsolutePath + link.Query.Replace("code=", "code=x"));

        Assert.Contains("Връзката е невалидна или е изтекла", page);
        Assert.Equal(AuthErrorCodes.EmailNotConfirmed, await (await client.LoginAsync(email)).ErrorCodeAsync());
    }
}
