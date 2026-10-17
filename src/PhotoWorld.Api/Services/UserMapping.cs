using PhotoWorld.Api.Data;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.Api.Services;

public static class UserMapping
{
    public static UserProfileDto ToDto(this User u) => new(
        u.Id, u.DisplayName, u.Email, u.AvatarUrl,
        u.ExternalLogins.Select(l => l.Provider).Order().ToList(),
        u.CreatedAt, u.LastLoginAt);
}
