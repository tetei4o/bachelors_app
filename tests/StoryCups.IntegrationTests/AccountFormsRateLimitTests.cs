using System.Net;
using System.Text.RegularExpressions;
using StoryCups.IntegrationTests.Infrastructure;

namespace StoryCups.IntegrationTests;

public sealed class AccountFormsLimitedAppFactory : StoryCupsAppFactory
{
    protected override int AccountFormPostsPerMinute => 3;
}

/// <summary>
/// Формите на страниците за акаунта в сайта имат общо ограничение на честотата по IP адрес (ADR-010).
/// Проверките са в един тест, защото броячът е общ за всички заявки от един адрес в рамките на минута.
/// </summary>
public sealed partial class AccountFormsRateLimitTests(AccountFormsLimitedAppFactory app)
    : IClassFixture<AccountFormsLimitedAppFactory>
{
    private const string TooManyRequestsPath = "/Account/TooManyRequests";

    [Fact]
    public async Task AccountForms_OverLimit_AreRedirectedAndNotProcessed()
    {
        var client = app.CreateClient(new() { AllowAutoRedirect = false });
        var email = ApiClient.NewEmail();

        // Регистрацията и потвърждението минават през API-то и по връзка (GET): не са изпратени форми.
        await client.RegisterAndConfirmAsync(app.Emails, email);

        // Три форми от три различни страници: лимитът е общ за страниците за акаунта.
        var login = await PostFormAsync(client, "/Account/Login", "login", new()
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "грешна парола",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("Грешен имейл или парола.", WebUtility.HtmlDecode(await login.Content.ReadAsStringAsync()));

        var forgot = await PostFormAsync(client, "/Account/ForgotPassword", "forgot-password", new() { ["Input.Email"] = email });
        Assert.Equal(HttpStatusCode.Redirect, forgot.StatusCode);
        Assert.Contains("Account/ForgotPasswordConfirmation", forgot.Headers.Location!.ToString());
        Assert.Single(app.Emails.SentTo(email), sent => sent.Kind == EmailKind.PasswordReset);

        var resend = await PostFormAsync(
            client, "/Account/ResendEmailConfirmation", "resend-email-confirmation", new() { ["Input.Email"] = email });
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        // Четвъртата форма в същата минута се отказва, преди да стигне до страницата: второ писмо няма.
        var rejected = await PostFormAsync(client, "/Account/ForgotPassword", "forgot-password", new() { ["Input.Email"] = email });
        Assert.Equal(HttpStatusCode.SeeOther, rejected.StatusCode);
        Assert.Equal(TooManyRequestsPath, rejected.Headers.Location!.ToString());
        Assert.Single(app.Emails.SentTo(email), sent => sent.Kind == EmailKind.PasswordReset);

        // Отварянето на страниците не се брои, а страницата с обяснението се показва нормално.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);
        var explanation = await client.GetAsync(TooManyRequestsPath);
        Assert.Equal(HttpStatusCode.OK, explanation.StatusCode);
        Assert.Contains("Твърде много опити", WebUtility.HtmlDecode(await explanation.Content.ReadAsStringAsync()));

        // Лимитът на формите не засяга API-то за приложението: то има собствена политика.
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(email)).StatusCode);
    }

    /// <summary>
    /// Изпраща форма като браузър: първо отваря страницата, за да вземе antiforgery токена и бисквитката му.
    /// </summary>
    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string path, string formName, Dictionary<string, string> fields)
    {
        var page = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var token = AntiforgeryToken().Match(await page.Content.ReadAsStringAsync());
        Assert.True(token.Success, $"Страницата {path} няма antiforgery токен.");

        fields["_handler"] = formName;
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
