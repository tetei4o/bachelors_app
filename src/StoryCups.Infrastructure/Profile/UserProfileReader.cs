using Microsoft.AspNetCore.Identity;
using StoryCups.Application.Profile;
using StoryCups.Contracts.Profile;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.Infrastructure.Profile;

public sealed class UserProfileReader(UserManager<ApplicationUser> userManager) : IUserProfileReader
{
    public async Task<ProfileDto?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return new ProfileDto(user.Id, user.Email ?? string.Empty, user.DisplayName, [.. roles.Order(StringComparer.Ordinal)]);
    }
}
