using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Api.Services.Identity;

/// <summary>
/// "Sign In with LinkedIn using OpenID Connect". LinkedIn's token introspection (authenticated with PhotoWorld's own
/// client credentials) says which app a token was issued to, so a token minted for another app can't be replayed;
/// then the OpenID userinfo endpoint gives the member's id, name, verified email and picture.
/// </summary>
public sealed class LinkedInIdentityVerifier(
    HttpClient http,
    IOptionsMonitor<ExternalProviderOptions> options,
    ILogger<LinkedInIdentityVerifier> logger) : IExternalIdentityVerifier
{
    public string Provider => AuthProviders.LinkedIn;

    public async Task<ExternalIdentity?> VerifyAsync(string accessToken, CancellationToken ct)
    {
        var client = options.Get(Provider);
        if (string.IsNullOrEmpty(client.ClientId) || string.IsNullOrEmpty(client.ClientSecret))
        {
            logger.LogWarning("LinkedIn sign-in attempted but Authentication:LinkedIn isn't configured on the API");
            return null;
        }

        using var introspect = await http.PostAsync("https://www.linkedin.com/oauth/v2/introspectToken", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["client_id"] = client.ClientId, ["client_secret"] = client.ClientSecret, ["token"] = accessToken }), ct);
        var info = introspect.IsSuccessStatusCode ? await introspect.Content.ReadFromJsonAsync<Introspection>(ct) : null;
        if (info is not { Active: true } || info.ClientId != client.ClientId)
        {
            logger.LogWarning("Rejected LinkedIn token (active: {Active}, issued to {ClientId})", info?.Active, info?.ClientId);
            return null;
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.linkedin.com/v2/userinfo");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        var user = await res.Content.ReadFromJsonAsync<UserInfo>(ct);
        if (user is null || string.IsNullOrEmpty(user.Sub)) return null;

        var name = user.Name ?? string.Join(' ', new[] { user.GivenName, user.FamilyName }.Where(n => !string.IsNullOrWhiteSpace(n)));
        return new ExternalIdentity(Provider, user.Sub, string.IsNullOrWhiteSpace(name) ? user.Email ?? "Photographer" : name,
            user.EmailVerified == true ? user.Email : null, user.Picture);
    }

    private sealed record Introspection(
        [property: JsonPropertyName("active")] bool Active,
        [property: JsonPropertyName("client_id")] string? ClientId);

    private sealed record UserInfo(
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("given_name")] string? GivenName,
        [property: JsonPropertyName("family_name")] string? FamilyName,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified")] bool? EmailVerified,
        [property: JsonPropertyName("picture")] string? Picture);
}
