using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>A member's own insights. Only ever about the caller; there's no route to anyone else's.</summary>
[ApiController]
[Authorize]
public class InsightsController(PhotoWorldDbContext db, SupabaseStorage storage, TimeProvider clock) : ControllerBase
{
    public const int Days = 30;

    [HttpGet(ApiRoutes.Insights)]
    public async Task<ActionResult<InsightsDto>> Get(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var from = today.AddDays(-(Days - 1));            // the last 30 days, today included
        var previousFrom = from.AddDays(-Days);
        var since = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var previousSince = since.AddDays(-Days);

        var views = db.PhotoViewDays.Where(v => v.Photo.OwnerId == me);
        var likes = db.PhotoLikes.Where(l => l.Photo.OwnerId == me && l.UserId != me);
        var follows = db.Follows.Where(f => f.FolloweeId == me);
        var comments = db.PhotoComments.Where(c => c.Photo.OwnerId == me && c.AuthorId != me);

        async Task<InsightStatDto> Stat(Func<DateTimeOffset, DateTimeOffset, Task<int>> count) =>
            new(await count(since, now.AddDays(1)), await count(previousSince, since));

        var viewStat = new InsightStatDto(
            await views.Where(v => v.Day >= from).SumAsync(v => (int?)v.Views, ct) ?? 0,
            await views.Where(v => v.Day >= previousFrom && v.Day < from).SumAsync(v => (int?)v.Views, ct) ?? 0);
        var likeStat = await Stat((a, b) => likes.CountAsync(l => l.CreatedAt >= a && l.CreatedAt < b, ct));
        var followStat = await Stat((a, b) => follows.CountAsync(f => f.CreatedAt >= a && f.CreatedAt < b, ct));
        var commentStat = await Stat((a, b) => comments.CountAsync(c => c.CreatedAt >= a && c.CreatedAt < b, ct));

        // Per day: views straight from the counters, likes grouped by their UTC date.
        var viewsByDay = await views.Where(v => v.Day >= from).GroupBy(v => v.Day)
            .Select(g => new { Day = g.Key, Views = g.Sum(v => v.Views) }).ToDictionaryAsync(d => d.Day, d => d.Views, ct);
        var likeTimes = await likes.Where(l => l.CreatedAt >= since).Select(l => l.CreatedAt).ToListAsync(ct);
        var likesByDay = likeTimes.GroupBy(t => DateOnly.FromDateTime(t.UtcDateTime)).ToDictionary(g => g.Key, g => g.Count());
        var days = Enumerable.Range(0, Days).Select(i => from.AddDays(i))
            .Select(d => new InsightDayDto(d, viewsByDay.GetValueOrDefault(d), likesByDay.GetValueOrDefault(d))).ToList();

        // Best photos of the period, by views then likes.
        var top = await db.Photos.Where(p => p.OwnerId == me)
            .Select(p => new
            {
                p.Id,
                Views = db.PhotoViewDays.Where(v => v.PhotoId == p.Id && v.Day >= from).Sum(v => (int?)v.Views) ?? 0,
                Likes = p.Likes.Count(l => l.CreatedAt >= since && l.UserId != me),
            })
            .Where(p => p.Views > 0 || p.Likes > 0)
            .OrderByDescending(p => p.Views).ThenByDescending(p => p.Likes).Take(5)
            .ToListAsync(ct);
        var ids = top.Select(t => t.Id).ToList();
        var summaries = (await db.Photos.AsNoTracking().Where(p => ids.Contains(p.Id)).ToSummariesAsync(me, storage, ct)).ToDictionary(p => p.Id);
        var topPhotos = top.Where(t => summaries.ContainsKey(t.Id)).Select(t => new InsightPhotoDto(summaries[t.Id], t.Views, t.Likes)).ToList();

        // Tags that earn the most likes (all time), on the member's photos. One row per tag and like, grouped by tag;
        // the DTO is built after the query, as EF can't translate a record constructor over a group.
        var tagStats = await (
                from t in db.PhotoTags.Where(t => t.Photo.OwnerId == me)
                join l in db.PhotoLikes.Where(l => l.UserId != me) on t.PhotoId equals l.PhotoId into tagLikes
                from l in tagLikes.DefaultIfEmpty()
                select new { t.Name, t.PhotoId, Liker = (Guid?)l.UserId })
            .GroupBy(x => x.Name)
            .Select(g => new { Tag = g.Key, Likes = g.Count(x => x.Liker != null), Photos = g.Select(x => x.PhotoId).Distinct().Count() })
            .OrderByDescending(t => t.Likes).ThenBy(t => t.Tag).Take(8)
            .ToListAsync(ct);
        var topTags = tagStats.Select(t => new InsightTagDto(t.Tag, t.Likes, t.Photos)).ToList();

        return new InsightsDto(viewStat, likeStat, followStat, commentStat,
            await follows.CountAsync(ct), await db.Photos.CountAsync(p => p.OwnerId == me, ct), days, topPhotos, topTags);
    }
}
