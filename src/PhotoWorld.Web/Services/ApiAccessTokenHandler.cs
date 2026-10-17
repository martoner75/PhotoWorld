using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using PhotoWorld.Web.Authentication;

namespace PhotoWorld.Web.Services;

/// <summary>Attaches the signed-in user's PhotoWorld API token to outgoing API calls.</summary>
public sealed class ApiAccessTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Headers.Authorization is null && accessor.HttpContext is { } http
            && await http.GetTokenAsync(AuthSchemes.ApiAccessToken) is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await base.SendAsync(request, ct);
    }
}
