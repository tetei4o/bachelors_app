using StoryCups.Contracts.Profile;

namespace StoryCups.Application.Profile;

/// <summary>
/// Чете профила на потребител по Id. Ползва се от API-то (/me) и от сайта.
/// </summary>
public interface IUserProfileReader
{
    Task<ProfileDto?> FindAsync(Guid userId, CancellationToken cancellationToken = default);
}
