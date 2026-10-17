namespace PhotoWorld.Api.Services.Identity;

/// <summary>The identity a provider vouched for after we validated the user's access token.</summary>
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
}
