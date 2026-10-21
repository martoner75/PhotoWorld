using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PhotoWorld.Shared.Activities;

namespace PhotoWorld.Api.Services.Push;

/// <summary>Sends to iPhones and iPads through APNs (HTTP/2, token-based auth). The signed token is reused for 50 minutes.</summary>
public sealed class ApnsChannel(HttpClient http, IOptions<PushOptions> options, TimeProvider clock, ILogger<ApnsChannel> logger) : IPushChannel
{
    private readonly ApnsOptions _o = options.Value.Apns;
    private readonly Lock _lock = new();
    private (string Jwt, DateTimeOffset At)? _token;
    private ECDsa? _key;

    public string Platform => DevicePlatforms.Ios;
    public bool IsConfigured => _o.IsConfigured;

    public async Task<PushResult> SendAsync(string token, PushMessage message, CancellationToken ct)
    {
        var host = _o.UseSandbox ? "https://api.sandbox.push.apple.com" : "https://api.push.apple.com";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{host}/3/device/{token}")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = new StringContent(PushFormats.ApnsPayload(message), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", ProviderToken());
        request.Headers.Add("apns-topic", _o.BundleId);
        request.Headers.Add("apns-push-type", "alert");
        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return PushResult.Sent;

        var body = await response.Content.ReadAsStringAsync(ct);
        if (PushFormats.IsDeadToken((int)response.StatusCode, body)) return PushResult.InvalidToken;
        logger.LogWarning("APNs refused a notification: {Status} {Body}", (int)response.StatusCode, body);
        return PushResult.Failed;
    }

    private string ProviderToken()
    {
        lock (_lock)
        {
            var now = clock.GetUtcNow();
            if (_token is { } t && now - t.At < TimeSpan.FromMinutes(50)) return t.Jwt;
            if (_key is null)
            {
                _key = ECDsa.Create();
                _key.ImportFromPem(_o.PrivateKey);
            }
            _token = (PushFormats.ApnsJwt(_o.TeamId, _o.KeyId, _key, now), now);
            return _token.Value.Jwt;
        }
    }
}

/// <summary>Sends to Android phones through Firebase Cloud Messaging HTTP v1, with an OAuth token from the service account.</summary>
public sealed class FcmChannel(HttpClient http, IOptions<PushOptions> options, TimeProvider clock, ILogger<FcmChannel> logger) : IPushChannel
{
    private readonly FcmOptions _o = options.Value.Fcm;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private ServiceAccount? _account;
    private RSA? _key;
    private (string Token, DateTimeOffset Expires)? _access;

    public string Platform => DevicePlatforms.Android;
    public bool IsConfigured => _o.IsConfigured;

    public async Task<PushResult> SendAsync(string token, PushMessage message, CancellationToken ct)
    {
        var account = Account();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{account.ProjectId}/messages:send")
        {
            Content = new StringContent(PushFormats.FcmPayload(token, message), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync(ct));
        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return PushResult.Sent;

        var body = await response.Content.ReadAsStringAsync(ct);
        if (PushFormats.IsDeadToken((int)response.StatusCode, body)) return PushResult.InvalidToken;
        logger.LogWarning("FCM refused a notification: {Status} {Body}", (int)response.StatusCode, body);
        return PushResult.Failed;
    }

    private ServiceAccount Account()
    {
        if (_account is not null) return _account;
        using var json = JsonDocument.Parse(_o.ServiceAccountJson);
        var root = json.RootElement;
        _account = new ServiceAccount(root.GetProperty("project_id").GetString()!, root.GetProperty("client_email").GetString()!,
            root.GetProperty("private_key").GetString()!,
            root.TryGetProperty("token_uri", out var uri) ? uri.GetString()! : "https://oauth2.googleapis.com/token");
        return _account;
    }

    private async Task<string> AccessTokenAsync(CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            var now = clock.GetUtcNow();
            if (_access is { } a && a.Expires > now.AddMinutes(1)) return a.Token;

            var account = Account();
            if (_key is null)
            {
                _key = RSA.Create();
                _key.ImportFromPem(account.PrivateKey);
            }
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = PushFormats.GoogleAssertion(account.ClientEmail, account.TokenUri, _key, now),
            });
            using var response = await http.PostAsync(account.TokenUri, form, ct);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var token = json.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = json.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
            _access = (token, now.AddSeconds(expiresIn));
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed record ServiceAccount(string ProjectId, string ClientEmail, string PrivateKey, string TokenUri);
}
