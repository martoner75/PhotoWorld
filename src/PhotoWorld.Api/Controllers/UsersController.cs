using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
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
    ILogger<UsersController> logger) : ControllerBase
{
    [HttpGet(ApiRoutes.CurrentUser)]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken ct)
    {
        if (User.GetUserId() is not { } id) return Unauthorized();

        var user = await db.Users.Include(u => u.ExternalLogins).SingleOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? NotFound() : user.ToDto();
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
                visiblePhotos.Count(p => p.OwnerId == u.Id), u.Id == me))
            .SingleOrDefaultAsync(ct);
        return member is null ? NotFound() : member;
    }
}
