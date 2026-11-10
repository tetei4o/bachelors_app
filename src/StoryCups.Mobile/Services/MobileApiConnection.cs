using StoryCups.Mobile.Auth;
using StoryCups.RestClient;

namespace StoryCups.Mobile.Services;

/// <summary>
/// Връзката на общия клиент на API-то (StoryCups.RestClient) с приложението: адресът идва от настройките,
/// а токенът от сесията, която го опреснява при нужда (ADR-004).
/// </summary>
public sealed class MobileApiConnection(ApiSettings settings, AuthSession session) : IApiConnection
{
    public Uri Resolve(string path) => settings.Resolve(path);

    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
        session.GetAccessTokenAsync(cancellationToken);

    public Task OnUnauthorizedAsync() => session.SignOutLocallyAsync();
}
