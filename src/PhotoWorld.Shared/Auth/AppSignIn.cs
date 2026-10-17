using System.Security.Cryptography;
using System.Text;

namespace PhotoWorld.Shared.Auth;

/// <summary>
/// How the iPhone/iPad app signs in through the website, PKCE-style: the app keeps a random verifier and sends only
/// its SHA-256 challenge. After Google sign-in the website redirects to <see cref="CallbackUri"/> with a short-lived
/// one-time code, and hands out the PhotoWorld token only to whoever presents the code with the matching verifier.
/// Another app that grabs the photoworld:// redirect gets a code it can't use.
/// </summary>
public static class AppSignIn
{
    public const string CallbackScheme = "photoworld";
    public const string CallbackUri = CallbackScheme + "://auth";

    /// <summary>Website path that starts the flow: <c>?provider=Google&amp;challenge=…</c>.</summary>
    public const string StartPath = "Account/AppLogin";

    /// <summary>Website path the app posts <see cref="AppTokenRequest"/> to.</summary>
    public const string TokenPath = "Account/AppToken";

    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(2);

    public static string NewVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string NewCode() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string ChallengeFor(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>A challenge is the base64url SHA-256 of a verifier: exactly 43 URL-safe characters.</summary>
    public static bool IsWellFormedChallenge(string? challenge) =>
        challenge is { Length: 43 } && challenge.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static bool VerifierMatches(string? verifier, string challenge) =>
        verifier is { Length: >= 43 and <= 128 }
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(ChallengeFor(verifier)), Encoding.ASCII.GetBytes(challenge));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed record AppTokenRequest(string Code, string Verifier);
