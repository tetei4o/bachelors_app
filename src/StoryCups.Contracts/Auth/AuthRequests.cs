using System.ComponentModel.DataAnnotations;

namespace StoryCups.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Имейлът е задължителен.")]
    [EmailAddress(ErrorMessage = "Невалиден имейл.")]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Паролата е задължителна.")]
    [StringLength(128, MinimumLength = 10, ErrorMessage = "Паролата трябва да е между {2} и {1} символа.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Името е задължително.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Името трябва да е между {2} и {1} символа.")]
    public string DisplayName { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "Нужно е съгласие с политиката за поверителност.")]
    public bool AcceptPrivacyPolicy { get; set; }
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Имейлът е задължителен.")]
    [EmailAddress(ErrorMessage = "Невалиден имейл.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Паролата е задължителна.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
