using System.ComponentModel.DataAnnotations;

namespace StoryCups.Infrastructure.Email;

/// <summary>
/// Настройки за изпращане на имейли. В разработката и в демото сочат към Mailpit (A16).
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 1025;

    /// <summary>
    /// STARTTLS. Mailpit работи без шифриране, затова по подразбиране е изключено.
    /// </summary>
    public bool UseStartTls { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    [Required, EmailAddress]
    public string FromAddress { get; set; } = "no-reply@example.com";

    [Required]
    public string FromName { get; set; } = "Story Cups";
}
