using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Api.Services.Identity;

public sealed class GoogleIdentityVerifier(
    HttpClient http,
    IOptionsMonitor<ExternalProviderOptions> options,
    ILogger<GoogleIdentityVerifier> logger) : IExternalIdentityVerifier
{
    public string Provider => AuthProviders.Google;

    public async Task<ExternalIdentity?> VerifyAsync(string accessToken, CancellationToken ct)
    {
        // tokeninfo tells us which client the token was issued to, so a token minted for
        // some other app can't be replayed against PhotoWorld.
        var info = await http.GetFromJsonAsync<TokenInfo>(
            $"https://oauth2.googleapis.com/tokeninfo?access_token={Uri.EscapeDataString(accessToken)}", ct);
        var clientId = options.Get(Provider).ClientId;
        if (info is null || string.IsNullOrEmpty(info.Sub) || (info.Aud != clientId && info.Azp != clientId))
        {
            logger.LogWarning("Rejected Google token issued to {Audience}", info?.Aud);
            return null;
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        var user = await res.Content.ReadFromJsonAsync<UserInfo>(ct);
        if (user is null || user.Sub != info.Sub) return null;

        return new ExternalIdentity(Provider, user.Sub, user.Name ?? user.Email ?? "Photographer",
            user.EmailVerified == true ? user.Email : null, user.Picture);
    }

    private sealed record TokenInfo(
        [property: JsonPropertyName("aud")] string? Aud,
        [property: JsonPropertyName("azp")] string? Azp,
        [property: JsonPropertyName("sub")] string? Sub);

    private sealed record UserInfo(
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified")] bool? EmailVerified,
        [property: JsonPropertyName("picture")] string? Picture);
}
