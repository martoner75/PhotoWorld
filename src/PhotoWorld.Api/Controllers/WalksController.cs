using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Walks;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// Photo walks: members organise meetups at a point on the map, others join, and photos tagged with the walk's tag
/// become its gallery. Walks by members the viewer blocked (or who blocked the viewer), or by suspended members, don't show.
/// </summary>
[ApiController]
[Authorize]
public class WalksController(PhotoWorldDbContext db, AdminAccess admins, ActivityFeed activity, SupabaseStorage storage,
    TimeProvider clock, ILogger<WalksController> logger) : ControllerBase
{
    private const int MaxListed = 100;

    [HttpGet(ApiRoutes.Walks)]
    public async Task<ActionResult<IReadOnlyList<WalkSummaryDto>>> List(string? list = null, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var now = clock.GetUtcNow();
        var query = Visible(me);
        query = list switch
        {
            WalkLists.Past => query.Where(w => w.EndsAt <= now).OrderByDescending(w => w.StartsAt),
            WalkLists.Mine => query.Where(w => w.OrganizerId == me || w.Attendees.Any(a => a.UserId == me)).OrderByDescending(w => w.StartsAt),
            _ => query.Where(w => w.EndsAt > now && w.CancelledAt == null).OrderBy(w => w.StartsAt),
        };
        return await SummariesAsync(query.Take(MaxListed), me, ct);
    }

    [HttpGet(ApiRoutes.Walk)]
    public async Task<ActionResult<WalkDto>> Get(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var summary = (await SummariesAsync(Visible(me).Where(w => w.Id == id), me, ct)).SingleOrDefault();
        if (summary is null) return NotFound();

        var details = await db.Walks.AsNoTracking().Where(w => w.Id == id)
            .Select(w => new
            {
                w.Description,
                Attendees = w.Attendees.Where(a => a.User.SuspendedAt == null && !a.User.BlockedBy.Any(b => b.BlockerId == me))
                    .OrderBy(a => a.JoinedAt).Select(a => new UserSummaryDto(a.User.Id, a.User.DisplayName, a.User.AvatarUrl)).ToList(),
                BlockedByOrganizer = db.Blocks.Any(b => b.BlockerId == w.OrganizerId && b.BlockedId == me),
            })
            .SingleAsync(ct);

        var tagged = db.Photos.AsNoTracking().Where(Moderation.PhotoVisibleTo(me)).Where(p => p.Tags.Any(t => t.Name == summary.Tag));
        var photoCount = await tagged.CountAsync(ct);
        var photos = await tagged.OrderByDescending(p => p.CreatedAt).Take(24).ToSummariesAsync(me, storage, ct);

        var reason = summary.IsGoing ? null : WalkRules.CannotJoin(summary.IsOrganizer, summary.IsCancelled, summary.StartsAt,
            clock.GetUtcNow(), summary.AttendeeCount, summary.MaxPeople, details.BlockedByOrganizer);
        var canEdit = summary.IsOrganizer || await admins.IsAdminAsync(me, ct);
        return new WalkDto(summary, details.Description, [summary.Organizer, .. details.Attendees], photos, photoCount, canEdit,
            !summary.IsGoing && reason is null, summary.IsGoing || summary.IsOrganizer ? null : reason);
    }

    /// <summary>Organises a walk; the organiser's followers hear about it.</summary>
    [HttpPost(ApiRoutes.Walks)]
    public async Task<ActionResult<WalkSummaryDto>> Create(SaveWalkRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!IsValid(request, isNew: true, attendees: 0)) return ValidationProblem(ModelState);

        var now = clock.GetUtcNow();
        var walk = new Walk { OrganizerId = me, Title = "", MeetingPoint = "", CreatedAt = now };
        Apply(walk, request, now);
        db.Walks.Add(walk);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} organised walk {WalkId}", me, walk.Id);
        var followers = await db.Follows.Where(f => f.FolloweeId == me).Select(f => f.FollowerId).ToListAsync(ct);
        await activity.NotifyWalkAsync(followers, me, me, walk.Id, ActivityTypes.WalkNew, ct);
        return CreatedAtAction(nameof(Get), new { id = walk.Id }, (await SummariesAsync(db.Walks.Where(w => w.Id == walk.Id), me, ct)).Single());
    }

    /// <summary>Changes a walk. Attendees hear about it when the time or meeting point moves.</summary>
    [HttpPut(ApiRoutes.Walk)]
    public async Task<ActionResult<WalkSummaryDto>> Update(Guid id, SaveWalkRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var walk = await db.Walks.Include(w => w.Attendees).SingleOrDefaultAsync(w => w.Id == id, ct);
        if (walk is null) return NotFound();
        if (walk.OrganizerId != me && !await admins.IsAdminAsync(me, ct)) return Forbid();
        if (!IsValid(request, isNew: false, attendees: walk.Attendees.Count)) return ValidationProblem(ModelState);

        var moved = walk.StartsAt != request.StartsAt || walk.EndsAt != request.EndsAt || walk.MeetingPoint != request.MeetingPoint.Trim()
                    || Math.Abs(walk.Latitude - request.Latitude) > 1e-6 || Math.Abs(walk.Longitude - request.Longitude) > 1e-6;
        if (walk.StartsAt != request.StartsAt) walk.ReminderSentAt = null; // remind again for the new time
        Apply(walk, request, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        if (moved && walk.CancelledAt is null)
            await activity.NotifyWalkAsync(walk.Attendees.Select(a => a.UserId).ToList(), me, walk.OrganizerId, id, ActivityTypes.WalkChanged, ct);
        return (await SummariesAsync(db.Walks.Where(w => w.Id == id), me, ct)).Single();
    }

    /// <summary>Calls a walk off (it stays listed as cancelled); attendees hear about it.</summary>
    [HttpPost(ApiRoutes.WalkCancel)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var walk = await db.Walks.Include(w => w.Attendees).SingleOrDefaultAsync(w => w.Id == id, ct);
        if (walk is null) return NotFound();
        if (walk.OrganizerId != me && !await admins.IsAdminAsync(me, ct)) return Forbid();
        if (walk.CancelledAt is not null) return NoContent();

        walk.CancelledAt = walk.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await activity.NotifyWalkAsync(walk.Attendees.Select(a => a.UserId).ToList(), me, walk.OrganizerId, id, ActivityTypes.WalkCancelled, ct);
        logger.LogInformation("User {UserId} cancelled walk {WalkId}", me, id);
        return NoContent();
    }

    /// <summary>Deletes a walk outright (organiser or admin). Photos tagged with it stay.</summary>
    [HttpDelete(ApiRoutes.Walk)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var organizer = await db.Walks.Where(w => w.Id == id).Select(w => (Guid?)w.OrganizerId).SingleOrDefaultAsync(ct);
        if (organizer is null) return NotFound();
        if (organizer != me && !await admins.IsAdminAsync(me, ct)) return Forbid();
        await db.Walks.Where(w => w.Id == id).ExecuteDeleteAsync(ct);
        logger.LogInformation("User {UserId} deleted walk {WalkId}", me, id);
        return NoContent();
    }

    [HttpPut(ApiRoutes.WalkAttendance)]
    public async Task<IActionResult> Join(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var walk = await Visible(me).Where(w => w.Id == id)
            .Select(w => new
            {
                w.OrganizerId, w.StartsAt, w.MaxPeople, Cancelled = w.CancelledAt != null, Count = w.Attendees.Count,
                Going = w.Attendees.Any(a => a.UserId == me),
                Blocked = db.Blocks.Any(b => b.BlockerId == w.OrganizerId && b.BlockedId == me),
            })
            .SingleOrDefaultAsync(ct);
        if (walk is null) return NotFound();
        if (walk.Going) return NoContent();
        if (WalkRules.CannotJoin(walk.OrganizerId == me, walk.Cancelled, walk.StartsAt, clock.GetUtcNow(), walk.Count, walk.MaxPeople,
                walk.Blocked) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        db.WalkAttendees.Add(new WalkAttendee { WalkId = id, UserId = me, JoinedAt = clock.GetUtcNow() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return NoContent(); /* joined twice at the same moment */ }
        await activity.NotifyAsync(walk.OrganizerId, me, ActivityTypes.WalkJoined, walkId: id, ct: ct);
        return NoContent();
    }

    [HttpDelete(ApiRoutes.WalkAttendance)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        await db.WalkAttendees.Where(a => a.WalkId == id && a.UserId == me).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    private bool IsValid(SaveWalkRequest r, bool isNew, int attendees)
    {
        foreach (var (field, message) in WalkRules.CannotSave(r.Title, r.MeetingPoint, r.StartsAt, r.EndsAt, clock.GetUtcNow(), isNew,
                     attendees, r.MaxPeople))
            ModelState.AddModelError(field, message);
        return ModelState.IsValid;
    }

    private static void Apply(Walk walk, SaveWalkRequest r, DateTimeOffset now)
    {
        walk.Title = r.Title.Trim();
        walk.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        walk.StartsAt = r.StartsAt.ToUniversalTime();
        walk.EndsAt = r.EndsAt.ToUniversalTime();
        walk.MeetingPoint = r.MeetingPoint.Trim();
        (walk.Latitude, walk.Longitude, walk.MaxPeople, walk.UpdatedAt) = (r.Latitude, r.Longitude, r.MaxPeople, now);
    }

    /// <summary>Walks the viewer may see: not by suspended organisers, nor between members where either blocked the other.</summary>
    private IQueryable<Walk> Visible(Guid me) => db.Walks.AsNoTracking()
        .Where(w => w.Organizer.SuspendedAt == null
                    && !w.Organizer.BlockedBy.Any(b => b.BlockerId == me)
                    && !db.Blocks.Any(b => b.BlockerId == w.OrganizerId && b.BlockedId == me));

    private async Task<List<WalkSummaryDto>> SummariesAsync(IQueryable<Walk> walks, Guid me, CancellationToken ct)
    {
        var visible = db.Photos.Where(Moderation.PhotoVisibleTo(me));
        var rows = await walks
            .Select(w => new
            {
                w.Id, w.Title, w.StartsAt, w.EndsAt, w.MeetingPoint, w.Latitude, w.Longitude, w.MaxPeople, w.CancelledAt, w.OrganizerId,
                Organizer = new UserSummaryDto(w.Organizer.Id, w.Organizer.DisplayName, w.Organizer.AvatarUrl),
                Count = w.Attendees.Count,
                Going = w.Attendees.Any(a => a.UserId == me),
            })
            .ToListAsync(ct);

        // Covers: the newest visible photo tagged with each walk's tag.
        var tags = rows.Select(r => WalkLimits.TagFor(r.Id)).ToList();
        var covers = (await db.PhotoTags.Where(t => tags.Contains(t.Name) && visible.Any(p => p.Id == t.PhotoId))
                .Select(t => new { t.Name, t.Photo.ThumbnailPath, t.Photo.CreatedAt }).ToListAsync(ct))
            .GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.CreatedAt).First().ThumbnailPath);

        return rows.Select(r =>
        {
            var tag = WalkLimits.TagFor(r.Id);
            return new WalkSummaryDto(r.Id, r.Title, r.StartsAt, r.EndsAt, r.MeetingPoint, r.Latitude, r.Longitude, r.Organizer,
                r.Count + 1, r.MaxPeople, r.Going, r.OrganizerId == me, r.CancelledAt is not null, tag,
                covers.TryGetValue(tag, out var path) ? storage.PublicUrl(path) : null);
        }).ToList();
    }
}
