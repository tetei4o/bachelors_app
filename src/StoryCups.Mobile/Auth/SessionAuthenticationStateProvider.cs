using Microsoft.AspNetCore.Components.Authorization;

namespace StoryCups.Mobile.Auth;

/// <summary>
/// Дава на Blazor компонентите (AuthorizeView, [Authorize]) потребителя от <see cref="AuthSession"/>.
/// </summary>
public sealed class SessionAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly AuthSession session;

    public SessionAuthenticationStateProvider(AuthSession session)
    {
        this.session = session;
        session.Changed += OnSessionChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(session.User));

    public void Dispose() => session.Changed -= OnSessionChanged;

    private void OnSessionChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
