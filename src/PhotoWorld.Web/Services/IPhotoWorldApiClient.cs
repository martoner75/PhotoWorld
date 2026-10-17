using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.Web.Services;

public interface IPhotoWorldApiClient
{
    /// <summary>Exchanges a provider access token for a PhotoWorld session. Null when the API rejects it.</summary>
    Task<AuthResponse?> ExternalLoginAsync(ExternalLoginRequest request, CancellationToken ct = default);

    /// <summary>The signed-in user's profile. Null when the API token is missing or expired.</summary>
    Task<UserProfileDto?> GetCurrentUserAsync(CancellationToken ct = default);
}
