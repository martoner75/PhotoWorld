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

namespace PhotoWorld.Api.Controllers;

/// <summary>The signed-in member's notifications and which kinds they want.</summary>
[ApiController]
[Authorize]
public class ActivityController(PhotoWorldDbContext db, ActivityFeed feed, SupabaseStorage storage, TimeProvider clock) : ControllerBase
{
    private const int PageSize = 30;

    /// <summary>Newest first. Items whose photo or post the member can no longer see, or by members they blocked, are left out.</summary>
    [HttpGet(ApiRoutes.Activity)]
    public async Task<ActionResult<PagedResult<ActivityDto>>> List(int page = 1, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        page = Math.Max(1, page);
        var query = Mine(me);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(a => new
            {
                a.Id, a.Type, a.PhotoId, a.PostId, a.ChallengeId, a.WalkId, a.ClubId, a.WorkshopId, a.CollabId, a.Place, a.Snippet, a.CreatedAt, a.ReadAt,
                Actor = a.Actor == null ? null : new UserSummaryDto(a.Actor.Id, a.Actor.DisplayName, a.Actor.AvatarUrl),
                PhotoTitle = a.Photo == null ? null : a.Photo.Title,
                Thumbnail = a.Photo == null ? null : a.Photo.ThumbnailPath,
                PostTitle = a.Post == null ? null : a.Post.Title,
                ChallengeTitle = a.Challenge == null ? null : a.Challenge.Title,
                WalkTitle = a.Walk == null ? null : a.Walk.Title,
                ClubName = a.Club == null ? null : a.Club.Name,
                WorkshopTitle = a.Workshop == null ? null : a.Workshop.Title,
                CollabTitle = a.Collab == null ? null : a.Collab.Title,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new ActivityDto(r.Id, r.Type, r.Actor, r.PhotoId, r.PhotoTitle,
            r.Thumbnail is null ? null : storage.ThumbnailUrl(r.Thumbnail), r.PostId, r.PostTitle, r.ChallengeId, r.ChallengeTitle,
            r.Place, r.Snippet, r.CreatedAt, r.ReadAt is not null, r.WalkId, r.WalkTitle, r.ClubId, r.ClubName,
            r.WorkshopId, r.WorkshopTitle, r.CollabId, r.CollabTitle)).ToList();
        return new PagedResult<ActivityDto>(items, page, PageSize, total);
    }

    [HttpGet(ApiRoutes.ActivityUnread)]
    public async Task<ActionResult<UnreadActivityDto>> Unread(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        return new UnreadActivityDto(await Mine(me).CountAsync(a => a.ReadAt == null, ct));
    }

    /// <summary>Marks everything read (the member opened their activity).</summary>
    [HttpPost(ApiRoutes.ActivityRead)]
    public async Task<IActionResult> MarkRead(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var now = clock.GetUtcNow();
        await db.Activities.Where(a => a.RecipientId == me && a.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ReadAt, now), ct);
        return NoContent();
    }

    [HttpGet(ApiRoutes.ActivitySettings)]
    public async Task<ActionResult<ActivitySettingsDto>> GetSettings(CancellationToken ct) =>
        User.GetUserId() is { } me ? await feed.SettingsAsync(me, ct) : Unauthorized();

    [HttpPut(ApiRoutes.ActivitySettings)]
    public async Task<ActionResult<ActivitySettingsDto>> SaveSettings(ActivitySettingsDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var settings = await db.ActivitySettings.FindAsync([me], ct);
        if (settings is null) db.ActivitySettings.Add(settings = new ActivitySettings { UserId = me });
        (settings.Likes, settings.Comments, settings.Follows, settings.FollowedPosts, settings.Challenges, settings.Push, settings.Walks,
            settings.Forecasts, settings.Clubs, settings.ClubPhotos, settings.Workshops, settings.Collabs) =
            (request.Likes, request.Comments, request.Follows, request.FollowedPosts, request.Challenges, request.Push, request.Walks,
            request.Forecasts, request.Clubs, request.ClubPhotos, request.Workshops, request.Collabs);
        await db.SaveChangesAsync(ct);
        return await feed.SettingsAsync(me, ct);
    }

    /// <summary>The member's activity that still makes sense to show.</summary>
    private IQueryable<Activity> Mine(Guid me)
    {
        var photos = db.Photos.Where(Moderation.PhotoVisibleTo(me));
        var posts = db.BlogPosts.Where(Moderation.PostVisibleTo(me));
        return db.Activities.AsNoTracking()
            .Where(a => a.RecipientId == me)
            .Where(a => a.ActorId == null
                        || (a.Actor!.SuspendedAt == null && !a.Actor.BlockedBy.Any(b => b.BlockerId == me)))
            .Where(a => a.PhotoId == null || photos.Any(p => p.Id == a.PhotoId))
            .Where(a => a.PostId == null || posts.Any(p => p.Id == a.PostId));
    }
}
