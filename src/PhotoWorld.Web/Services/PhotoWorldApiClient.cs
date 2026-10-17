using System.Net;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.Web.Services;

public sealed class PhotoWorldApiClient(HttpClient http, ILogger<PhotoWorldApiClient> logger) : IPhotoWorldApiClient
{
    public async Task<AuthResponse?> ExternalLoginAsync(ExternalLoginRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync(ApiRoutes.ExternalLogin, request, ct);
        if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<AuthResponse>(ct);

        logger.LogWarning("API rejected {Provider} login: {Status} {Body}",
            request.Provider, (int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));
        return null;
    }

    public async Task<UserProfileDto?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        using var res = await http.GetAsync(ApiRoutes.CurrentUser, ct);
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<UserProfileDto>(ct);
    }
}
