using StoryCups.Contracts;
using StoryCups.Contracts.Profile;

namespace StoryCups.Mobile.Services;

/// <summary>
/// IProfileService за приложението: чете профила от /api/v1/me.
/// </summary>
public sealed class HttpProfileService(ApiClient api) : IProfileService
{
    public Task<ProfileDto?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<ProfileDto>(ApiRoutes.Profile.Me, cancellationToken);
}
