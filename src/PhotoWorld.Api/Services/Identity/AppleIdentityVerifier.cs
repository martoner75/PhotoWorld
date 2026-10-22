using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Api.Services.Identity;

/// <summary>
/// Sign in with Apple. The website sends Apple's ID token (a JWT), which is checked here against Apple's published
/// signing keys: issued by Apple, for PhotoWorld's Services ID, not expired. Apple puts no name in the token (the
/// website passes the one Apple sent on first sign-in) and never shares a picture.
/// </summary>
public sealed class AppleIdentityVerifier(
    AppleSigningKeys keys,
    IOptionsMonitor<ExternalProviderOptions> options,
    ILogger<AppleIdentityVerifier> logger) : IExternalIdentityVerifier
{
    public const string Issuer = "https://appleid.apple.com";

    public string Provider => AuthProviders.Apple;

    public async Task<ExternalIdentity?> VerifyAsync(string idToken, CancellationToken ct)
    {
        var clientId = options.Get(Provider).ClientId;
        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("Apple sign-in attempted but Authentication:Apple isn't configured on the API");
            return null;
        }
        var result = await ValidateAsync(idToken, await keys.GetAsync(ct), clientId);
        if (!result.IsValid)
        {
            logger.LogWarning(result.Exception, "Rejected Apple ID token");
            return null;
        }
        return ToIdentity(result.ClaimsIdentity);
    }

    internal static Task<TokenValidationResult> ValidateAsync(string idToken, IEnumerable<SecurityKey> signingKeys, string clientId) =>
        new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = clientId,
            IssuerSigningKeys = signingKeys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromMinutes(2),
        });

    internal static ExternalIdentity? ToIdentity(ClaimsIdentity claims)
    {
        var sub = claims.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(sub)) return null;
        // email_verified comes as "true" or true; Apple only issues verified (or relay) addresses.
        var verified = string.Equals(claims.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        var email = verified ? claims.FindFirst("email")?.Value : null;
        return new ExternalIdentity(AuthProviders.Apple, sub, DisplayName: "", email, AvatarUrl: null);
    }
}

/// <summary>Apple's signing keys, from its OpenID configuration; cached and refreshed by the configuration manager.</summary>
public sealed class AppleSigningKeys
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _config = new(
        $"{AppleIdentityVerifier.Issuer}/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever { RequireHttps = true });

    public async Task<IEnumerable<SecurityKey>> GetAsync(CancellationToken ct) => (await _config.GetConfigurationAsync(ct)).SigningKeys;
}

/// <summary>
/// Revokes a member's Sign in with Apple authorisation (Apple requires it when an account is deleted), so PhotoWorld
/// disappears from "Sign in with Apple" in their Apple account settings.
/// </summary>
public sealed class AppleTokenRevoker(HttpClient http, IOptionsMonitor<ExternalProviderOptions> options, TimeProvider clock, ILogger<AppleTokenRevoker> logger)
{
    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var o = options.Get(AuthProviders.Apple);
        if (string.IsNullOrEmpty(o.ClientId) || string.IsNullOrEmpty(o.TeamId) || string.IsNullOrEmpty(o.KeyId) || string.IsNullOrEmpty(o.PrivateKey))
        {
            logger.LogWarning("Can't revoke an Apple sign-in: Authentication:Apple isn't fully configured on the API");
            return;
        }
        using var res = await http.PostAsync($"{AppleIdentityVerifier.Issuer}/auth/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = o.ClientId,
            ["client_secret"] = AppleClientSecret.Create(o.TeamId, o.KeyId, o.ClientId, o.PrivateKey, clock.GetUtcNow()),
            ["token"] = refreshToken,
            ["token_type_hint"] = "refresh_token",
        }), ct);
        if (!res.IsSuccessStatusCode) logger.LogWarning("Apple refused to revoke a sign-in: {Status}", (int)res.StatusCode);
    }
}
