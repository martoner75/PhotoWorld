using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;

namespace PhotoWorld.Web.Authentication;

/// <summary>
/// OAuth handler for "Instagram API with Instagram Login". The only quirk vs. the generic
/// handler is that Instagram may wrap the token response as <c>{"data":[{"access_token":...}]}</c>.
/// </summary>
public sealed class InstagramAuthenticationHandler(
    IOptionsMonitor<OAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : OAuthHandler<OAuthOptions>(options, logger, encoder)
{
    protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        var response = await base.ExchangeCodeAsync(context);
        if (response.Error is not null || response.AccessToken is not null) return response;

        if (response.Response?.RootElement.TryGetProperty("data", out var data) == true
            && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
        {
            return OAuthTokenResponse.Success(JsonDocument.Parse(data[0].GetRawText()));
        }
        return response;
    }
}
