using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Admin;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>The admins' Users page: find members, make them admins, suspend or reactivate accounts.</summary>
[ApiController]
[Authorize]
public class AdminUsersController(
    PhotoWorldDbContext db,
    AdminAccess admins,
    IMemoryCache cache,
    TimeProvider clock,
    ILogger<AdminUsersController> logger) : ControllerBase
{
    private const int PageSize = 50;

    /// <summary>Members, newest first, optionally those whose name or email contains <paramref name="search"/> (any case).</summary>
    [HttpGet(ApiRoutes.AdminUsers)]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> List(int page = 1, string? search = null, CancellationToken ct = default)
    {
        if (!await admins.IsAdminAsync(User.GetUserId(), ct)) return Forbid();
        page = Math.Max(1, page);

        var query = db.Users.AsNoTracking();
        if (search?.Trim().ToLowerInvariant() is { Length: > 0 } term)
        {
            if (term.Length > AdminLimits.SearchMax) term = term[..AdminLimits.SearchMax];
            query = query.Where(u => u.DisplayName.ToLower().Contains(term) || (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(u => new
            {
                u.Id, u.DisplayName, u.Email, u.AvatarUrl, u.CreatedAt, u.LastLoginAt, u.IsAdmin, u.SuspendedAt, u.SuspensionReason,
                Photos = db.Photos.Count(p => p.OwnerId == u.Id),
                Posts = db.BlogPosts.Count(p => p.AuthorId == u.Id && p.Status == BlogPostStatus.Published),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminUserDto(r.Id, r.DisplayName, r.Email, r.AvatarUrl, r.CreatedAt, r.LastLoginAt,
            r.Photos, r.Posts, r.IsAdmin, r.SuspendedAt, r.SuspensionReason)).ToList();
        return new PagedResult<AdminUserDto>(items, page, PageSize, total);
    }

    [HttpPut(ApiRoutes.AdminUserAdmin)]
    public async Task<IActionResult> SetAdmin(Guid id, SetAdminRequest request, CancellationToken ct)
    {
        var me = User.GetUserId();
        if (!await admins.IsAdminAsync(me, ct)) return Forbid();
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();
        if (AdminRules.CannotChangeAdmin(me, id, request.IsAdmin, user.SuspendedAt is not null) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        user.IsAdmin = request.IsAdmin;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} {Change} admin rights for member {UserId}", me, request.IsAdmin ? "granted" : "removed", id);
        return NoContent();
    }

    [HttpPut(ApiRoutes.AdminUserSuspension)]
    public async Task<IActionResult> SetSuspension(Guid id, SetSuspensionRequest request, CancellationToken ct)
    {
        var me = User.GetUserId();
        if (!await admins.IsAdminAsync(me, ct)) return Forbid();
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();
        if (AdminRules.CannotSuspend(me, id, request.Suspended) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        if (request.Suspended)
        {
            user.SuspendedAt ??= clock.GetUtcNow();
            user.SuspensionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            user.IsAdmin = false; // a suspended member loses admin rights; give them back by hand if reactivated
        }
        else
        {
            user.SuspendedAt = null;
            user.SuspensionReason = null;
        }
        await db.SaveChangesAsync(ct);
        SessionCheck.Forget(cache, id);

        logger.LogInformation("Admin {AdminId} {Change} member {UserId}", me, request.Suspended ? "suspended" : "reactivated", id);
        return NoContent();
    }
}
