using Microsoft.Extensions.Options;

namespace StoryCups.Infrastructure.Email;

/// <summary>
/// Връзките в писмата за акаунта. Кодовете идват вече Base64Url-кодирани, затова са безопасни в адрес.
/// </summary>
public sealed class AccountLinks(IOptions<AppUrlOptions> options)
{
    private readonly Uri baseUri = options.Value.GetBaseUri();

    public string Login() => Build("Account/Login");

    public string ConfirmEmail(Guid userId, string encodedCode) =>
        Build($"Account/ConfirmEmail?userId={userId}&code={encodedCode}");

    public string ResetPassword(string encodedCode) => Build($"Account/ResetPassword?code={encodedCode}");

    private string Build(string relative) => new Uri(baseUri, relative).AbsoluteUri;
}
