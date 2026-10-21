using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PhotoWorld.Api.Services.Push;

/// <summary>A notification for one phone: what to show, the app's badge number, and what to open when tapped.</summary>
public sealed record PushMessage(string Title, string Body, int? Badge, IReadOnlyDictionary<string, string> Data);

public enum PushResult { Sent, InvalidToken, Failed }

/// <summary>One push service (Apple or Google).</summary>
public interface IPushChannel
{
    string Platform { get; }
    bool IsConfigured { get; }
    Task<PushResult> SendAsync(string token, PushMessage message, CancellationToken ct);
}

/// <summary>The request bodies and signed tokens the push services expect, kept apart so they're easy to test.</summary>
public static class PushFormats
{
    public static string ApnsPayload(PushMessage m)
    {
        var aps = new JsonObject
        {
            ["alert"] = new JsonObject { ["title"] = m.Title, ["body"] = m.Body },
            ["sound"] = "default",
        };
        if (m.Badge is { } badge) aps["badge"] = badge;
        var root = new JsonObject { ["aps"] = aps };
        foreach (var (key, value) in m.Data) root[key] = value;
        return root.ToJsonString();
    }

    public static string FcmPayload(string token, PushMessage m)
    {
        var data = new JsonObject();
        foreach (var (key, value) in m.Data) data[key] = value;
        var android = new JsonObject { ["priority"] = "high" };
        if (m.Badge is { } badge) android["notification"] = new JsonObject { ["notification_count"] = badge };
        return new JsonObject
        {
            ["message"] = new JsonObject
            {
                ["token"] = token,
                ["notification"] = new JsonObject { ["title"] = m.Title, ["body"] = m.Body },
                ["data"] = data,
                ["android"] = android,
            },
        }.ToJsonString();
    }

    /// <summary>APNs provider token: ES256 JWT with the key id in the header and the team id as issuer.</summary>
    public static string ApnsJwt(string teamId, string keyId, ECDsa key, DateTimeOffset now) =>
        Jwt(new JsonObject { ["alg"] = "ES256", ["kid"] = keyId },
            new JsonObject { ["iss"] = teamId, ["iat"] = now.ToUnixTimeSeconds() },
            data => key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

    /// <summary>Google service-account assertion (RS256), exchanged for an OAuth access token to call FCM.</summary>
    public static string GoogleAssertion(string clientEmail, string tokenUri, RSA key, DateTimeOffset now) =>
        Jwt(new JsonObject { ["alg"] = "RS256", ["typ"] = "JWT" },
            new JsonObject
            {
                ["iss"] = clientEmail,
                ["scope"] = "https://www.googleapis.com/auth/firebase.messaging",
                ["aud"] = tokenUri,
                ["iat"] = now.ToUnixTimeSeconds(),
                ["exp"] = now.AddHours(1).ToUnixTimeSeconds(),
            },
            data => key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    private static string Jwt(JsonObject header, JsonObject claims, Func<byte[], byte[]> sign)
    {
        var unsigned = $"{Base64Url(Encoding.UTF8.GetBytes(header.ToJsonString()))}.{Base64Url(Encoding.UTF8.GetBytes(claims.ToJsonString()))}";
        return $"{unsigned}.{Base64Url(sign(Encoding.ASCII.GetBytes(unsigned)))}";
    }

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Whether a push service's error says the token is dead (the app was uninstalled, or the token rotated).</summary>
    public static bool IsDeadToken(int status, string body) =>
        status is 404 or 410
        || (status == 400 && (body.Contains("BadDeviceToken", StringComparison.Ordinal) || body.Contains("Unregistered", StringComparison.Ordinal)
                              || body.Contains("UNREGISTERED", StringComparison.Ordinal)));
}
