using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PhotoWorld.Api.Data;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Api.Services;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "PhotoWorld.Api";
    public string Audience { get; set; } = "PhotoWorld";
    public string SigningKey { get; set; } = "";
    public int LifetimeMinutes { get; set; } = 480;

    public SymmetricSecurityKey GetKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(User user, string provider)
    {
        var o = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(o.LifetimeMinutes);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(PhotoWorldClaimTypes.Provider, provider),
        ];
        if (user.Email is not null) claims.Add(new(JwtRegisteredClaimNames.Email, user.Email));
        if (user.AvatarUrl is not null) claims.Add(new(PhotoWorldClaimTypes.AvatarUrl, user.AvatarUrl));

        var token = new JwtSecurityToken(
            o.Issuer, o.Audience, claims,
            notBefore: now.UtcDateTime, expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(o.GetKey(), SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
