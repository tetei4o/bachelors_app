using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using StoryCups.Application.Profile;
using StoryCups.Contracts.Profile;

namespace StoryCups.Web.Services;

/// <summary>
/// IProfileService за сайта: чете директно от базата, без HTTP.
/// Мобилното приложение има своя реализация, която вика /api/v1/me.
/// </summary>
internal sealed class ServerProfileService(
    AuthenticationStateProvider authenticationStateProvider,
    IServiceScopeFactory scopeFactory) : IProfileService
{
    public async Task<ProfileDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var id = state.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var userId))
        {
            return null;
        }

        // Отделен scope за всяко четене: интерактивната връзка живее дълго, а DbContext не бива.
        await using var scope = scopeFactory.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IUserProfileReader>();
        return await reader.FindAsync(userId, cancellationToken);
    }
}
