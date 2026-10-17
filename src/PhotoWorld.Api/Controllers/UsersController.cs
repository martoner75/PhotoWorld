using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.Api.Controllers;

[ApiController]
[Authorize]
public class UsersController(PhotoWorldDbContext db) : ControllerBase
{
    [HttpGet(ApiRoutes.CurrentUser)]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();

        var user = await db.Users.Include(u => u.ExternalLogins).SingleOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? NotFound() : user.ToDto();
    }
}
