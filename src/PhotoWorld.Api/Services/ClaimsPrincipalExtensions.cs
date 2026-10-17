using System.Security.Claims;

namespace PhotoWorld.Api.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The PhotoWorld user id from the JWT <c>sub</c> claim (mapped to NameIdentifier by JwtBearer).</summary>
    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
