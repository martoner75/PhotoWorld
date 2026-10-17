using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.App.Services;

/// <summary>
/// The signed-in member's PhotoWorld session. Sign-in runs through the website in Apple's secure browser sheet
/// (see <see cref="AppSignIn"/>); the token is kept in the iOS Keychain via <see cref="SecureStorage"/>.
/// </summary>
public sealed class AuthService(ILogger<AuthService> logger)
{
    private const string SessionKey = "photoworld.session";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private AuthResponse? _session;

    /// <summary>Raised when the session ends: signed out, deleted, or the API stopped accepting the token.</summary>
    public event EventHandler? SignedOut;

    public UserProfileDto? User => IsSignedIn ? _session!.User : null;

    // A minute of slack so a request doesn't leave with a token that expires on the way.
    public bool IsSignedIn => _session is not null && _session.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1);

    public string? AccessToken => IsSignedIn ? _session!.AccessToken : null;

    /// <summary>Picks up the session saved by an earlier launch, if it's still valid.</summary>
    public async Task<bool> RestoreAsync()
    {
        try
        {
            var saved = await SecureStorage.Default.GetAsync(SessionKey);
            _session = saved is null ? null : JsonSerializer.Deserialize<AuthResponse>(saved, Json);
        }
        catch (Exception ex)
        {
            // A Keychain or format problem just means signing in again.
            logger.LogWarning(ex, "Could not restore the saved session");
            _session = null;
        }
        return IsSignedIn;
    }

    /// <summary>
    /// Signs in with Google through the website. Throws <see cref="TaskCanceledException"/> if the member closes the
    /// sheet, and <see cref="SignInException"/> if sign-in fails.
    /// </summary>
    public async Task SignInAsync(CancellationToken ct = default)
    {
        var verifier = AppSignIn.NewVerifier();
        var start = new Uri(AppConfig.WebBaseUrl,
            $"{AppSignIn.StartPath}?provider={AuthProviders.Google}&challenge={AppSignIn.ChallengeFor(verifier)}");

        var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = start,
            CallbackUrl = new Uri(AppSignIn.CallbackUri),
        });

        if (result.Properties.TryGetValue("error", out var error)) throw new SignInException(error);
        if (!result.Properties.TryGetValue("code", out var code)) throw new SignInException("Sign-in didn't complete. Please try again.");

        using var http = new HttpClient(AppConfig.CreateHttpHandler()) { BaseAddress = AppConfig.WebBaseUrl };
        using var response = await http.PostAsJsonAsync(AppSignIn.TokenPath, new AppTokenRequest(code, verifier), Json, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Website refused the sign-in code: {Status}", (int)response.StatusCode);
            throw new SignInException("Sign-in expired. Please try again.");
        }

        _session = await response.Content.ReadFromJsonAsync<AuthResponse>(Json, ct)
                   ?? throw new SignInException("Sign-in didn't complete. Please try again.");
        await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(_session, Json));
    }

    public void SignOut()
    {
        _session = null;
        SecureStorage.Default.Remove(SessionKey);
        SignedOut?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class SignInException(string message) : Exception(message);
