using System.Text.Json.Serialization;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Api.Services.Identity;

/// <summary>
/// Validates tokens from "Instagram API with Instagram Login".
/// Note: since Meta retired the Basic Display API (Dec 2024) this only works for
/// Instagram Business/Creator accounts, and Instagram does not expose email addresses.
/// </summary>
public sealed class InstagramIdentityVerifier(HttpClient http) : IExternalIdentityVerifier
{
    public string Provider => AuthProviders.Instagram;

    public async Task<ExternalIdentity?> VerifyAsync(string accessToken, CancellationToken ct)
    {
        // Instagram user tokens are scoped to the app that requested them, so a successful
        // /me call with our token is the validation step (there is no debug_token equivalent).
        using var res = await http.GetAsync(
            $"https://graph.instagram.com/v21.0/me?fields=user_id,username,name,profile_picture_url&access_token={Uri.EscapeDataString(accessToken)}", ct);
        if (!res.IsSuccessStatusCode) return null;

        var me = await res.Content.ReadFromJsonAsync<Me>(ct);
        if (me is null || string.IsNullOrEmpty(me.UserId)) return null;

        return new ExternalIdentity(Provider, me.UserId, me.Name ?? me.Username ?? "Photographer", null, me.ProfilePictureUrl);
    }

    private sealed record Me(
        [property: JsonPropertyName("user_id")] string? UserId,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("profile_picture_url")] string? ProfilePictureUrl);
}
