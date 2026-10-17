using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Api.Services.Identity;

public sealed class FacebookIdentityVerifier(
    HttpClient http,
    IOptionsMonitor<ExternalProviderOptions> options,
    ILogger<FacebookIdentityVerifier> logger) : IExternalIdentityVerifier
{
    private const string Graph = "https://graph.facebook.com/v21.0";

    public string Provider => AuthProviders.Facebook;

    public async Task<ExternalIdentity?> VerifyAsync(string accessToken, CancellationToken ct)
    {
        var o = options.Get(Provider);
        var appToken = $"{o.ClientId}|{o.ClientSecret}";

        // debug_token confirms the token is valid and belongs to our app.
        var debug = await http.GetFromJsonAsync<DebugTokenEnvelope>(
            $"{Graph}/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={Uri.EscapeDataString(appToken)}", ct);
        if (debug?.Data is not { IsValid: true } d || d.AppId != o.ClientId || string.IsNullOrEmpty(d.UserId))
        {
            logger.LogWarning("Rejected Facebook token for app {AppId}", debug?.Data?.AppId);
            return null;
        }

        var me = await http.GetFromJsonAsync<Me>(
            $"{Graph}/me?fields=id,name,email,picture.type(large)&access_token={Uri.EscapeDataString(accessToken)}", ct);
        if (me is null || me.Id != d.UserId) return null;

        return new ExternalIdentity(Provider, me.Id, me.Name ?? "Photographer", me.Email, me.Picture?.Data?.Url);
    }

    private sealed record DebugTokenEnvelope([property: JsonPropertyName("data")] DebugToken? Data);

    private sealed record DebugToken(
        [property: JsonPropertyName("app_id")] string? AppId,
        [property: JsonPropertyName("user_id")] string? UserId,
        [property: JsonPropertyName("is_valid")] bool IsValid);

    private sealed record Me(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("picture")] PictureEnvelope? Picture);

    private sealed record PictureEnvelope([property: JsonPropertyName("data")] PictureData? Data);

    private sealed record PictureData([property: JsonPropertyName("url")] string? Url);
}
