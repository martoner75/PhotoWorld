using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// Feedback on photos whose owners ask for it: ratings for composition, light and focus plus a comment. Private between
/// the giver and the owner; everyone else only sees how many members gave feedback.
/// </summary>
[ApiController]
[Authorize]
public class FeedbackController(PhotoWorldDbContext db, ActivityFeed activity, TimeProvider clock) : ControllerBase
{
    [HttpGet(ApiRoutes.PhotoFeedback)]
    public async Task<ActionResult<PhotoFeedbackDto>> Get(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var photo = await Visible(me).Where(p => p.Id == id).Select(p => new { p.OwnerId, p.WantsFeedback }).SingleOrDefaultAsync(ct);
        if (photo is null) return NotFound();
        var isOwner = photo.OwnerId == me;

        // Feedback from members the viewer blocked, or who are suspended, isn't shown (or counted) for them.
        var feedback = db.PhotoFeedback.AsNoTracking().Where(f => f.PhotoId == id)
            .Where(f => f.Author.SuspendedAt == null && !f.Author.BlockedBy.Any(b => b.BlockerId == me));
        var count = await feedback.CountAsync(ct);
        var items = await feedback.Where(f => isOwner || f.AuthorId == me)
            .OrderByDescending(f => f.UpdatedAt)
            .Select(f => new FeedbackDto(f.Id, new UserSummaryDto(f.Author.Id, f.Author.DisplayName, f.Author.AvatarUrl),
                f.Composition, f.Light, f.Focus, f.Comment, f.UpdatedAt, f.AuthorId == me))
            .ToListAsync(ct);

        var averages = isOwner && items.Count > 0
            ? new FeedbackAveragesDto(items.Average(f => f.Composition), items.Average(f => f.Light), items.Average(f => f.Focus))
            : null;
        var canGive = !isOwner && photo.WantsFeedback && !await BlockedByOwnerAsync(photo.OwnerId, me, ct);
        return new PhotoFeedbackDto(photo.WantsFeedback, count, averages, isOwner ? items : [], items.FirstOrDefault(f => f.IsMine), canGive);
    }

    /// <summary>Gives or changes the caller's feedback. The owner hears about the first one.</summary>
    [HttpPut(ApiRoutes.PhotoFeedback)]
    public async Task<IActionResult> Save(Guid id, SaveFeedbackRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var photo = await Visible(me).Where(p => p.Id == id).Select(p => new { p.OwnerId, p.WantsFeedback }).SingleOrDefaultAsync(ct);
        if (photo is null) return NotFound();
        if (photo.OwnerId == me) return Problem("You can't give feedback on your own photo.", statusCode: StatusCodes.Status400BadRequest);
        if (!photo.WantsFeedback) return Problem("This photo isn't asking for feedback.", statusCode: StatusCodes.Status400BadRequest);
        if (await BlockedByOwnerAsync(photo.OwnerId, me, ct))
            return Problem("You can't give feedback on this photo.", statusCode: StatusCodes.Status400BadRequest);

        var now = clock.GetUtcNow();
        var mine = await db.PhotoFeedback.SingleOrDefaultAsync(f => f.PhotoId == id && f.AuthorId == me, ct);
        if (mine is null) db.PhotoFeedback.Add(mine = new PhotoFeedback { PhotoId = id, AuthorId = me, CreatedAt = now });
        (mine.Composition, mine.Light, mine.Focus) = (request.Composition, request.Light, request.Focus);
        mine.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        mine.UpdatedAt = now;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Problem("Your feedback was saved from another device a moment ago. Reload to see it.", statusCode: StatusCodes.Status409Conflict);
        }
        await activity.NotifyAsync(photo.OwnerId, me, ActivityTypes.Feedback, photoId: id, snippet: mine.Comment, ct: ct);
        return NoContent();
    }

    /// <summary>Takes back the caller's own feedback.</summary>
    [HttpDelete(ApiRoutes.PhotoFeedback)]
    public async Task<IActionResult> DeleteMine(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        await db.PhotoFeedback.Where(f => f.PhotoId == id && f.AuthorId == me).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    /// <summary>The photo's owner removes someone's feedback.</summary>
    [HttpDelete(ApiRoutes.PhotoFeedbackItem)]
    public async Task<IActionResult> Delete(Guid id, Guid feedbackId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var deleted = await db.PhotoFeedback.Where(f => f.Id == feedbackId && f.PhotoId == id && (f.Photo.OwnerId == me || f.AuthorId == me))
            .ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }

    private IQueryable<Photo> Visible(Guid me) => db.Photos.AsNoTracking().Where(Moderation.PhotoVisibleTo(me));

    private Task<bool> BlockedByOwnerAsync(Guid ownerId, Guid me, CancellationToken ct) =>
        db.Blocks.AnyAsync(b => b.BlockerId == ownerId && b.BlockedId == me, ct);
}
