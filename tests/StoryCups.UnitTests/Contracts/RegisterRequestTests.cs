using System.ComponentModel.DataAnnotations;
using StoryCups.Contracts.Auth;

namespace StoryCups.UnitTests.Contracts;

public sealed class RegisterRequestTests
{
    [Theory]
    [InlineData(PasswordRules.MinLength - 1, false)]
    [InlineData(PasswordRules.MinLength, true)]
    [InlineData(PasswordRules.MaxLength, true)]
    [InlineData(PasswordRules.MaxLength + 1, false)]
    public void Password_LengthLimits(int length, bool valid)
    {
        var request = new RegisterRequest
        {
            Email = "client@example.com",
            Password = new string('а', length),
            DisplayName = "Тестов клиент",
            AcceptPrivacyPolicy = true,
        };

        var errors = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.Equal(valid, isValid);
    }
}
