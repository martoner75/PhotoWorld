using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PhotoWorld.Api.Services.Push;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Tests;

public class PushTests
{
    private static readonly PushMessage Message = new("PhotoCommunity", "Anna liked your photo “Fog”", 3,
        new Dictionary<string, string> { ["photoId"] = "p1", ["type"] = "like" });

    [Fact]
    public void Apns_payload_has_alert_badge_sound_and_data()
    {
        using var json = JsonDocument.Parse(PushFormats.ApnsPayload(Message));
        var aps = json.RootElement.GetProperty("aps");
        Assert.Equal("Anna liked your photo “Fog”", aps.GetProperty("alert").GetProperty("body").GetString());
        Assert.Equal(3, aps.GetProperty("badge").GetInt32());
        Assert.Equal("default", aps.GetProperty("sound").GetString());
        Assert.Equal("p1", json.RootElement.GetProperty("photoId").GetString());
    }

    [Fact]
    public void Fcm_payload_targets_the_token_with_string_data()
    {
        using var json = JsonDocument.Parse(PushFormats.FcmPayload("tok", Message));
        var message = json.RootElement.GetProperty("message");
        Assert.Equal("tok", message.GetProperty("token").GetString());
        Assert.Equal("PhotoCommunity", message.GetProperty("notification").GetProperty("title").GetString());
        Assert.Equal("like", message.GetProperty("data").GetProperty("type").GetString());
        Assert.Equal("high", message.GetProperty("android").GetProperty("priority").GetString());
    }

    private static (JsonElement Header, JsonElement Claims, byte[] Signed, byte[] Signature) Split(string jwt)
    {
        var parts = jwt.Split('.');
        static byte[] Decode(string s) => Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
        return (JsonDocument.Parse(Decode(parts[0])).RootElement, JsonDocument.Parse(Decode(parts[1])).RootElement,
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Decode(parts[2]));
    }

    [Fact]
    public void Apns_token_is_a_verifiable_es256_jwt()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var (header, claims, signed, signature) = Split(PushFormats.ApnsJwt("TEAM123", "KEY456", key, now));

        Assert.Equal("ES256", header.GetProperty("alg").GetString());
        Assert.Equal("KEY456", header.GetProperty("kid").GetString());
        Assert.Equal("TEAM123", claims.GetProperty("iss").GetString());
        Assert.Equal(1_800_000_000, claims.GetProperty("iat").GetInt64());
        Assert.True(key.VerifyData(signed, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    public void Google_assertion_is_a_verifiable_rs256_jwt_for_fcm()
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var (header, claims, signed, signature) = Split(PushFormats.GoogleAssertion("svc@x.iam.gserviceaccount.com", "https://oauth2.googleapis.com/token", key, now));

        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal("svc@x.iam.gserviceaccount.com", claims.GetProperty("iss").GetString());
        Assert.Equal("https://www.googleapis.com/auth/firebase.messaging", claims.GetProperty("scope").GetString());
        Assert.Equal(1_800_003_600, claims.GetProperty("exp").GetInt64());
        Assert.True(key.VerifyData(signed, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Theory]
    [InlineData(410, "", true)]
    [InlineData(404, "", true)]
    [InlineData(400, "{\"reason\":\"BadDeviceToken\"}", true)]
    [InlineData(400, "{\"error\":{\"status\":\"INVALID_ARGUMENT\",\"details\":[{\"errorCode\":\"UNREGISTERED\"}]}}", true)]
    [InlineData(400, "{\"reason\":\"PayloadTooLarge\"}", false)]
    [InlineData(500, "", false)]
    public void Dead_tokens_are_recognised(int status, string body, bool dead)
    {
        Assert.Equal(dead, PushFormats.IsDeadToken(status, body));
    }

    [Fact]
    public void Push_text_matches_the_activity_screen_and_opens_the_photo()
    {
        var activity = new ActivityDto(Guid.NewGuid(), ActivityTypes.Comment, new UserSummaryDto(Guid.NewGuid(), "Anna", null),
            Guid.NewGuid(), "Fog", null, null, null, null, null, null, "Lovely light", DateTimeOffset.UtcNow, false);
        var message = PushText.For(activity, unread: 2);

        Assert.Equal("Anna commented on your photo “Fog”: “Lovely light”", message.Body);
        Assert.Equal(2, message.Badge);
        Assert.Equal(activity.PhotoId.ToString(), message.Data["photoId"]);
        Assert.Null(PushText.For(activity, unread: 0).Badge);
    }
}
