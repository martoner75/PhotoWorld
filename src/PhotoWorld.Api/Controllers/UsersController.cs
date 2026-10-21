using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Challenges;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.Api.Controllers;

[ApiController]
[Authorize]
public class UsersController(
    PhotoWorldDbContext db,
    SupabaseStorage storage,
    ChallengeStandings standings,
    AdminAccess admins,
    TimeProvider clock,
    ILogger<UsersController> logger) : ControllerBase
{
    [HttpGet(ApiRoutes.CurrentUser)]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken ct)
    {
        if (User.GetUserId() is not { } id) return Unauthorized();

        var user = await db.Users.Include(u => u.ExternalLogins).SingleOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? NotFound() : user.ToDto() with { IsAdmin = await admins.IsAdminAsync(user.Id, ct) };
    }

    /// <summary>
    /// Deletes the signed-in member and everything they shared: photos and post PDFs (and their files), likes, comments
    /// and linked sign-ins. Database rows go first, in one statement via cascades; files are removed after.
    /// </summary>
    [HttpDelete(ApiRoutes.CurrentUser)]
    public async Task<IActionResult> DeleteMe(CancellationToken ct)
    {
        if (User.GetUserId() is not { } id) return Unauthorized();
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();

        var files = (await db.Photos.Where(p => p.OwnerId == id)
                .Select(p => new { p.DisplayPath, p.ThumbnailPath })
                .ToListAsync(ct))
            .SelectMany(p => new[] { p.DisplayPath, p.ThumbnailPath })
            .ToList();

        var attachments = await db.BlogAttachments.Where(a => a.Post.AuthorId == id).Select(a => a.StoragePath).ToListAsync(ct);

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);

        // Storage deletes take up to 1,000 paths per call.
        foreach (var (bucket, paths) in new[] { ((string?)null, files), (storage.AttachmentBucket, attachments) })
        foreach (var chunk in paths.Chunk(500))
        {
            try
            {
                await storage.DeleteAsync(chunk, CancellationToken.None, bucket);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Could not delete {Count} storage files for deleted user {UserId}", chunk.Length, id);
            }
        }

        logger.LogInformation("User {UserId} deleted their account ({PhotoCount} photos)", id, files.Count / 2);
        return NoContent();
    }

    /// <summary>A member's public profile. Unlike <see cref="Me"/>, it never includes the email address.</summary>
    [HttpGet(ApiRoutes.Member)]
    public async Task<ActionResult<MemberProfileDto>> Member(Guid id, CancellationToken ct)
    {
        var me = User.GetUserId();
        var visiblePhotos = db.Photos.Where(Moderation.PhotoVisibleTo(me));
        var member = await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new MemberProfileDto(u.Id, u.DisplayName, u.AvatarUrl, u.CreatedAt,
                visiblePhotos.Count(p => p.OwnerId == u.Id), u.Id == me,
                db.Follows.Count(f => f.FolloweeId == u.Id),
                db.Follows.Count(f => f.FollowerId == u.Id),
                db.Follows.Any(f => f.FolloweeId == u.Id && f.FollowerId == me),
                null,
                db.Blocks.Any(b => b.BlockerId == me && b.BlockedId == u.Id)))
            .SingleOrDefaultAsync(ct);
        return member is null ? NotFound() : member with { Wins = await WinsAsync(id, ct) };
    }

    [HttpPost(ApiRoutes.MemberFollow)]
    public async Task<ActionResult<FollowStateDto>> Follow(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (id == me) return Problem("You can't follow yourself.", statusCode: StatusCodes.Status400BadRequest);
        if (!await db.Users.AnyAsync(u => u.Id == id, ct)) return NotFound();
        if (await db.Blocks.AnyAsync(b => (b.BlockerId == id && b.BlockedId == me) || (b.BlockerId == me && b.BlockedId == id), ct))
            return Problem("You can't follow this member.", statusCode: StatusCodes.Status400BadRequest);

        if (!await db.Follows.AnyAsync(f => f.FollowerId == me && f.FolloweeId == id, ct))
        {
            db.Follows.Add(new Follow { FollowerId = me, FolloweeId = id, CreatedAt = clock.GetUtcNow() });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { /* Followed twice at the same moment: the follow already exists. */ }
        }
        return await FollowStateAsync(id, me, ct);
    }

    [HttpDelete(ApiRoutes.MemberFollow)]
    public async Task<ActionResult<FollowStateDto>> Unfollow(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!await db.Users.AnyAsync(u => u.Id == id, ct)) return NotFound();

        await db.Follows.Where(f => f.FollowerId == me && f.FolloweeId == id).ExecuteDeleteAsync(ct);
        return await FollowStateAsync(id, me, ct);
    }

    /// <summary>
    /// Blocks a member: the caller stops seeing their photos, posts and comments, they can't follow the caller or
    /// comment on the caller's photos and posts, and any follows between the two are removed.
    /// </summary>
    [HttpPost(ApiRoutes.MemberBlock)]
    public async Task<IActionResult> Block(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (id == me) return Problem("You can't block yourself.", statusCode: StatusCodes.Status400BadRequest);
        if (!await db.Users.AnyAsync(u => u.Id == id, ct)) return NotFound();

        if (!await db.Blocks.AnyAsync(b => b.BlockerId == me && b.BlockedId == id, ct))
        {
            db.Blocks.Add(new Block { BlockerId = me, BlockedId = id, CreatedAt = clock.GetUtcNow() });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { /* Blocked twice at the same moment: the block already exists. */ }
        }
        await db.Follows.Where(f => (f.FollowerId == me && f.FolloweeId == id) || (f.FollowerId == id && f.FolloweeId == me))
            .ExecuteDeleteAsync(ct);

        logger.LogInformation("User {UserId} blocked user {BlockedId}", me, id);
        return NoContent();
    }

    [HttpDelete(ApiRoutes.MemberBlock)]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        await db.Blocks.Where(b => b.BlockerId == me && b.BlockedId == id).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    [HttpGet(ApiRoutes.MyBlocks)]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> MyBlocks(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        return await db.Blocks.AsNoTracking()
            .Where(b => b.BlockerId == me)
            .OrderBy(b => b.Blocked.DisplayName)
            .Select(b => new UserSummaryDto(b.Blocked.Id, b.Blocked.DisplayName, b.Blocked.AvatarUrl))
            .ToListAsync(ct);
    }

    private async Task<FollowStateDto> FollowStateAsync(Guid id, Guid me, CancellationToken ct) => new(
        await db.Follows.AnyAsync(f => f.FollowerId == me && f.FolloweeId == id, ct),
        await db.Follows.CountAsync(f => f.FolloweeId == id, ct));

    /// <summary>Closed challenges this member won, newest first.</summary>
    private async Task<IReadOnlyList<ChallengeWinDto>> WinsAsync(Guid id, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // Only challenges they entered can be wins, which keeps the scoring to a handful.
        var entered = await db.Challenges.AsNoTracking()
            .Where(c => c.EndsAt <= now && c.Entries.Any(e => e.Photo.OwnerId == id))
            .Select(c => new { c.Id, c.Title, c.EndsAt })
            .ToListAsync(ct);
        if (entered.Count == 0) return [];

        var scores = await standings.ForAsync(entered.Select(c => c.Id).ToList(), ct);
        return entered
            .Select(c => (Challenge: c, Winner: scores.GetValueOrDefault(c.Id, Standing.Empty).Ranked.FirstOrDefault()))
            .Where(w => w.Winner?.OwnerId == id)
            .OrderByDescending(w => w.Challenge.EndsAt)
            .Select(w => new ChallengeWinDto(w.Challenge.Id, w.Challenge.Title, w.Challenge.EndsAt, w.Winner!.PhotoId))
            .ToList();
    }
}
