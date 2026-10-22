namespace PhotoWorld.Api.Services.Identity;

/// <summary>
/// The identity a provider vouched for after we validated the user's token. <see cref="DisplayName"/> is empty when the
/// provider doesn't share one (Apple, after the first sign-in).
/// </summary>
public sealed record ExternalIdentity(
    string Provider,
    string ProviderUserId,
    string DisplayName,
    string? Email,
    string? AvatarUrl);

public interface IExternalIdentityVerifier
{
    string Provider { get; }

    /// <summary>Returns the identity behind <paramref name="accessToken"/>, or null if the token is invalid or not issued to PhotoWorld.</summary>
    Task<ExternalIdentity?> VerifyAsync(string accessToken, CancellationToken ct);
}

public sealed class ExternalProviderOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    // Apple only: no fixed secret; requests are signed with the team's Sign in with Apple key (.p8).
    public string? TeamId { get; set; }
    public string? KeyId { get; set; }
    public string? PrivateKey { get; set; }
}
