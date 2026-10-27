using Microsoft.AspNetCore.Identity;

namespace StoryCups.Infrastructure.Identity;

/// <summary>
/// Потребител на системата. Пазим минимални данни (A19): имейл, хеш на паролата, име за показване, дата на съгласието.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 50;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Кога потребителят е приел политиката за поверителност. Персоналът няма такава дата.
    /// </summary>
    public DateTimeOffset? PrivacyConsentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
