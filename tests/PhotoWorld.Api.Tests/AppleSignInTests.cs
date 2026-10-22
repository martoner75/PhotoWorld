using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PhotoWorld.Api.Controllers;
using PhotoWorld.Api.Services.Identity;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Api.Tests;

public class AppleSignInTests
{
    private const string ClientId = "com.photoworld.web";
    private static readonly RsaSecurityKey AppleKey = new(RSA.Create(2048)) { KeyId = "apple-test" };

    private static string IdToken(string audience = ClientId, string issuer = AppleIdentityVerifier.Issuer, object? emailVerified = null,
        DateTime? expires = null, SecurityKey? key = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Expires = expires ?? DateTime.UtcNow.AddMinutes(10),
            IssuedAt = DateTime.UtcNow,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "001234.abcd",
                ["email"] = "ana@example.com",
                ["email_verified"] = emailVerified ?? "true",
            },
            SigningCredentials = new SigningCredentials(key ?? AppleKey, SecurityAlgorithms.RsaSha256),
        });

    [Fact]
    public async Task A_valid_Apple_token_gives_the_member_and_their_verified_email()
    {
        var result = await AppleIdentityVerifier.ValidateAsync(IdToken(), [AppleKey], ClientId);

        Assert.True(result.IsValid, result.Exception?.Message);
        var identity = AppleIdentityVerifier.ToIdentity(result.ClaimsIdentity)!;
        Assert.Equal(("Apple", "001234.abcd", "ana@example.com"), (identity.Provider, identity.ProviderUserId, identity.Email));
        Assert.Equal("", identity.DisplayName);
    }

    [Fact]
    public async Task Email_verified_may_be_a_boolean()
    {
        var result = await AppleIdentityVerifier.ValidateAsync(IdToken(emailVerified: true), [AppleKey], ClientId);
        Assert.Equal("ana@example.com", AppleIdentityVerifier.ToIdentity(result.ClaimsIdentity)!.Email);
    }

    [Fact]
    public async Task An_unverified_email_is_not_used()
    {
        var result = await AppleIdentityVerifier.ValidateAsync(IdToken(emailVerified: "false"), [AppleKey], ClientId);
        Assert.Null(AppleIdentityVerifier.ToIdentity(result.ClaimsIdentity)!.Email);
    }

    [Fact]
    public async Task Tokens_for_another_app_from_another_issuer_expired_or_not_signed_by_Apple_are_rejected()
    {
        Assert.False((await AppleIdentityVerifier.ValidateAsync(IdToken(audience: "com.someone.else"), [AppleKey], ClientId)).IsValid);
        Assert.False((await AppleIdentityVerifier.ValidateAsync(IdToken(issuer: "https://evil.example"), [AppleKey], ClientId)).IsValid);
        Assert.False((await AppleIdentityVerifier.ValidateAsync(IdToken(expires: DateTime.UtcNow.AddMinutes(-10)), [AppleKey], ClientId)).IsValid);
        var forged = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "apple-test" };
        Assert.False((await AppleIdentityVerifier.ValidateAsync(IdToken(key: forged), [AppleKey], ClientId)).IsValid);
    }

    [Fact]
    public void Client_secret_is_an_ES256_JWT_for_Apple_signed_with_the_team_key()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var jwt = AppleClientSecret.Create("TEAM123", "KEY456", ClientId, key.ExportPkcs8PrivateKeyPem(), now);

        var parts = jwt.Split('.');
        static JsonElement Json(string part) => JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(part)).RootElement;
        Assert.Equal(("ES256", "KEY456"), (Json(parts[0]).GetProperty("alg").GetString(), Json(parts[0]).GetProperty("kid").GetString()));
        var claims = Json(parts[1]);
        Assert.Equal("TEAM123", claims.GetProperty("iss").GetString());
        Assert.Equal(ClientId, claims.GetProperty("sub").GetString());
        Assert.Equal("https://appleid.apple.com", claims.GetProperty("aud").GetString());
        Assert.True(claims.GetProperty("exp").GetInt64() > now.ToUnixTimeSeconds());
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64UrlEncoder.DecodeBytes(parts[2]),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Theory]
    [InlineData("Ana Silva", null, "ana@example.com", "Ana Silva")]          // the provider's name wins
    [InlineData("", "Ana Silva", "x@privaterelay.appleid.com", "Ana Silva")] // Apple's first-sign-in name
    [InlineData("", null, "ana.silva@example.com", "ana.silva")]             // the start of a real email
    [InlineData("", null, "x1y2@privaterelay.appleid.com", "Photographer")]  // never the relay address
    [InlineData("", "  ", null, "Photographer")]
    public void New_members_get_the_best_name_available(string providerName, string? suggested, string? email, string expected)
    {
        var identity = new ExternalIdentity("Apple", "sub", providerName, email, null);
        Assert.Equal(expected, AuthController.NewMemberName(identity, suggested));
    }
}
