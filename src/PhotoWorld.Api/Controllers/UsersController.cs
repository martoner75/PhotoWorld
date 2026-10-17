using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.Api.Controllers;

[ApiController]
[Authorize]
public class UsersController(PhotoWorldDbContext db) : ControllerBase
{
    [HttpGet(ApiRoutes.CurrentUser)]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken ct)
    {
        if (User.GetUserId() is not { } id) return Unauthorized();

        var user = await db.Users.Include(u => u.ExternalLogins).SingleOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? NotFound() : user.ToDto();
    }

    /// <summary>A member's public profile. Unlike <see cref="Me"/>, it never includes the email address.</summary>
    [HttpGet(ApiRoutes.Member)]
    public async Task<ActionResult<MemberProfileDto>> Member(Guid id, CancellationToken ct)
    {
        var me = User.GetUserId();
        var member = await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new MemberProfileDto(u.Id, u.DisplayName, u.AvatarUrl, u.CreatedAt,
                db.Photos.Count(p => p.OwnerId == u.Id), u.Id == me))
            .SingleOrDefaultAsync(ct);
        return member is null ? NotFound() : member;
    }
}
