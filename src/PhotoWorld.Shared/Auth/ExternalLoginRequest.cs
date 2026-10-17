using System.ComponentModel.DataAnnotations;

namespace PhotoWorld.Shared.Auth;

/// <summary>
/// Sent by the UI to the API after a successful OAuth round-trip with an external provider.
/// The API never trusts the UI's word for who the user is: it re-validates
/// <see cref="AccessToken"/> directly with the provider before signing the user in.
/// </summary>
public sealed record ExternalLoginRequest(
    [Required] string Provider,
    [Required] string AccessToken);
