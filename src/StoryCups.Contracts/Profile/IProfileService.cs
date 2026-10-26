namespace StoryCups.Contracts.Profile;

/// <summary>
/// Източник на данни за профила за общите компоненти (ADR-003).
/// Сайтът го реализира директно към сървъра, мобилното приложение през /api/v1/me.
/// </summary>
public interface IProfileService
{
    Task<ProfileDto?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
