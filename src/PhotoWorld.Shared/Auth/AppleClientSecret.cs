using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace PhotoWorld.Shared.Auth;

/// <summary>
/// Sign in with Apple has no fixed client secret: each request carries a short ES256 JWT signed with the team's
/// "Sign in with Apple" key (.p8). Used by the website (exchanging the sign-in code) and the API (revoking tokens when
/// a member deletes their account).
/// </summary>
public static class AppleClientSecret
{
    public const string Audience = "https://appleid.apple.com";

    /// <param name="clientId">The Services ID used for the website sign-in (for example com.photoworld.web).</param>
    /// <param name="privateKey">The .p8 file's contents (PEM, "-----BEGIN PRIVATE KEY-----").</param>
    public static string Create(string teamId, string keyId, string clientId, string privateKey, DateTimeOffset now)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(privateKey);
        var header = new JsonObject { ["alg"] = "ES256", ["kid"] = keyId };
        var claims = new JsonObject
        {
            ["iss"] = teamId,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(), // used straight away; Apple allows up to 6 months
            ["aud"] = Audience,
            ["sub"] = clientId,
        };
        var unsigned = $"{Base64Url(Encoding.UTF8.GetBytes(header.ToJsonString()))}.{Base64Url(Encoding.UTF8.GetBytes(claims.ToJsonString()))}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{unsigned}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
