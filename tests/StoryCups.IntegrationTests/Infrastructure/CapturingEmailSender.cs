using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Identity;
using StoryCups.Infrastructure.Email;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.IntegrationTests.Infrastructure;

public enum EmailKind
{
    Confirmation,
    PasswordReset,
    AccountExists,
}

public sealed record SentEmail(string To, EmailKind Kind, string Link);

/// <summary>
/// Пази писмата в паметта вместо да ги праща по SMTP.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender<ApplicationUser>, IAccountNotifier
{
    private readonly ConcurrentQueue<SentEmail> sent = new();

    public IReadOnlyList<SentEmail> SentTo(string email) =>
        sent.Where(e => string.Equals(e.To, email, StringComparison.OrdinalIgnoreCase)).ToList();

    public SentEmail Last(string email, EmailKind kind) => SentTo(email).Last(e => e.Kind == kind);

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        Add(email, EmailKind.Confirmation, confirmationLink);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        Add(email, EmailKind.PasswordReset, resetLink);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        throw new NotSupportedException("Приложението праща връзка, не код.");

    public Task SendAccountExistsAsync(string email, string loginLink) =>
        Add(email, EmailKind.AccountExists, loginLink);

    private Task Add(string email, EmailKind kind, string htmlEncodedLink)
    {
        sent.Enqueue(new SentEmail(email, kind, WebUtility.HtmlDecode(htmlEncodedLink)));
        return Task.CompletedTask;
    }
}
