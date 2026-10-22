using System.ComponentModel.DataAnnotations;

namespace PhotoWorld.Shared.Auth;

/// <summary>
/// Sent by the UI to the API after a successful OAuth round-trip with an external provider.
/// The API never trusts the UI's word for who the user is: it re-validates
/// <see cref="AccessToken"/> directly with the provider before signing the user in.
/// </summary>
/// <param name="AccessToken">The provider's access token; for Apple, its signed ID token.</param>
/// <param name="DisplayName">
/// Apple only: the name Apple sends the very first time a member signs in (it isn't in the token). Used as the new
/// account's name, never to rename an existing one.
/// </param>
/// <param name="RefreshToken">Apple only: kept so the API can revoke the sign-in when the member deletes their account.</param>
public sealed record ExternalLoginRequest(
    [Required] string Provider,
    [Required] string AccessToken,
    [StringLength(100)] string? DisplayName = null,
    [StringLength(2000)] string? RefreshToken = null);
