namespace StoryCups.Infrastructure.Email;

/// <summary>
/// Писма извън договора на IEmailSender от Identity.
/// </summary>
public interface IAccountNotifier
{
    Task SendAccountExistsAsync(string email, string loginLink);
}
