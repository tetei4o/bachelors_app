using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.Infrastructure.Email;

/// <summary>
/// Изпраща имейлите на ASP.NET Core Identity през SMTP с MailKit.
/// Връзките идват вече HTML-кодирани от извикващия код (договорът на IEmailSender), затова не ги кодираме повторно.
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    : IEmailSender<ApplicationUser>, IAccountNotifier
{
    private readonly SmtpOptions options = options.Value;

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(
            email,
            "Потвърдете имейла си",
            $"""
            <p>Здравейте, {Encode(user.DisplayName)}!</p>
            <p>За да активирате картата си за лоялност, потвърдете имейла си:
            <a href="{confirmationLink}">потвърждавам</a>.</p>
            <p>Ако не сте се регистрирали вие, пренебрегнете това писмо.</p>
            """);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(
            email,
            "Нова парола",
            $"""
            <p>Здравейте, {Encode(user.DisplayName)}!</p>
            <p>Получихме заявка за нова парола. Задайте я оттук: <a href="{resetLink}">нова парола</a>.</p>
            <p>Ако не сте поискали смяна, пренебрегнете това писмо; паролата ви остава същата.</p>
            """);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(
            email,
            "Код за нова парола",
            $"""
            <p>Здравейте, {Encode(user.DisplayName)}!</p>
            <p>Кодът ви за нова парола е: <strong>{Encode(resetCode)}</strong></p>
            """);

    /// <summary>
    /// Писмо до вече регистриран имейл при нов опит за регистрация.
    /// Така формата не издава кои имейли имат акаунт.
    /// </summary>
    public Task SendAccountExistsAsync(string email, string loginLink) =>
        SendAsync(
            email,
            "Опит за регистрация",
            $"""
            <p>Здравейте!</p>
            <p>Някой се опита да създаде акаунт с този имейл, но такъв вече съществува.
            Ако сте били вие, влезте оттук: <a href="{loginLink}">вход</a>.
            Ако сте забравили паролата си, използвайте „Забравена парола“.</p>
            """);

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var security = options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(options.Host, options.Port, security);
        if (!string.IsNullOrEmpty(options.UserName))
        {
            await client.AuthenticateAsync(options.UserName, options.Password ?? string.Empty);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);
        logger.LogInformation("Изпратен имейл \"{Subject}\".", subject);
    }

    private static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value);
}
