using PhotoWorld.Shared.Users;

namespace PhotoWorld.Shared.Auth;

/// <summary>
/// Returned by the API after a successful external login.
/// <see cref="AccessToken"/> is a PhotoWorld JWT the UI uses for subsequent API calls.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    bool IsNewUser,
    UserProfileDto User);
