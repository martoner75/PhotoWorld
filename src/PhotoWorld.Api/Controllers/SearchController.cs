using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>Search across members, tags, photos and posts, and the Explore page. Everything goes through the visibility rules.</summary>
[ApiController]
[Authorize]
public class SearchController(PhotoWorldDbContext db, SupabaseStorage storage, TimeProvider clock) : ControllerBase
{
    public const int MinQuery = 2;

    [HttpGet(ApiRoutes.Search)]
    public async Task<ActionResult<SearchResultsDto>> Search(string? q, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var query = (q ?? "").Trim();
        if (query.StartsWith('#')) query = query.TrimStart('#');
        if (query.Length < MinQuery) return new SearchResultsDto(query, [], [], [], 0, []);
        if (query.Length > PhotoSearch.TextMax) query = query[..PhotoSearch.TextMax];
        var term = query.ToLowerInvariant();
        var asTag = PhotoTags.Normalize(term);

        var members = await MembersAsync(db.Users.Where(u => u.SuspendedAt == null && u.DisplayName.ToLower().Contains(term)
                                                             && !u.BlockedBy.Any(b => b.BlockerId == me)), me, 8, ct);

        var visible = db.Photos.Where(Moderation.PhotoVisibleTo(me));
        var tags = asTag is null ? [] : await db.PhotoTags
            .Where(t => t.Name.Contains(asTag) && visible.Any(p => p.Id == t.PhotoId))
            .GroupBy(t => t.Name).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(10)
            .Select(g => new TagCountDto(g.Key, g.Count())).ToListAsync(ct);

        var photos = visible.Matching(new PhotoSearch { Text = query });
        var photoCount = await photos.CountAsync(ct);
        var photoItems = await photos.OrderByDescending(p => p.CreatedAt).Take(12).ToSummariesAsync(me, storage, ct);

        var visiblePhotos = db.Photos.Where(Moderation.PhotoVisibleTo(me));
        var posts = (await db.BlogPosts.AsNoTracking()
                .Where(Moderation.PostVisibleTo(me))
                .Where(p => p.Status == BlogPostStatus.Published
                            && (p.Title.ToLower().Contains(term) || p.Excerpt.ToLower().Contains(term)))
                .OrderByDescending(p => p.PublishedAt).Take(6)
                .Select(p => new
                {
                    p.Id, p.Title, p.Excerpt, p.Status, p.PublishedAt, p.UpdatedAt,
                    Author = new UserSummaryDto(p.Author.Id, p.Author.DisplayName, p.Author.AvatarUrl),
                    Cover = visiblePhotos.Where(ph => ph.Id == p.CoverPhotoId).Select(ph => ph.ThumbnailPath).FirstOrDefault(),
                    Comments = p.Comments.AsQueryable().Count(Moderation.BlogCommentVisibleTo(me)),
                })
                .ToListAsync(ct))
            .Select(p => new BlogPostSummaryDto(p.Id, p.Title, p.Excerpt, p.Cover is null ? null : storage.ThumbnailUrl(p.Cover),
                p.Author, p.Comments, p.Status, p.PublishedAt, p.UpdatedAt, ModerationState.Visible)).ToList();

        return new SearchResultsDto(query, members, tags, photoItems, photoCount, posts);
    }

    /// <summary>
    /// Trending tags (most used on photos shared in the last week), popular photos from the last two weeks by members
    /// the viewer doesn't follow, and members to follow (most followed, then most photos).
    /// </summary>
    [HttpGet(ApiRoutes.Explore)]
    public async Task<ActionResult<ExploreDto>> Explore(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var now = clock.GetUtcNow();
        var visible = db.Photos.Where(Moderation.PhotoVisibleTo(me));

        var weekAgo = now.AddDays(-7);
        var trending = await db.PhotoTags
            .Where(t => t.Photo.CreatedAt >= weekAgo && visible.Any(p => p.Id == t.PhotoId))
            .GroupBy(t => t.Name).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(12)
            .Select(g => new TagCountDto(g.Key, g.Count())).ToListAsync(ct);
        if (trending.Count < 6)
        {
            // A quiet week: fill up with the most used tags overall.
            var known = trending.Select(t => t.Name).ToList();
            trending.AddRange(await db.PhotoTags.Where(t => !known.Contains(t.Name) && visible.Any(p => p.Id == t.PhotoId))
                .GroupBy(t => t.Name).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(12 - trending.Count)
                .Select(g => new TagCountDto(g.Key, g.Count())).ToListAsync(ct));
        }

        var twoWeeksAgo = now.AddDays(-14);
        var popular = await visible
            .Where(p => p.OwnerId != me && !db.Follows.Any(f => f.FollowerId == me && f.FolloweeId == p.OwnerId))
            .Where(p => p.CreatedAt >= twoWeeksAgo)
            .OrderByDescending(p => p.Likes.Count).ThenByDescending(p => p.CreatedAt).Take(24)
            .ToSummariesAsync(me, storage, ct);
        if (popular.Count < 12)
        {
            var shown = popular.Select(p => p.Id).ToList();
            popular.AddRange(await visible
                .Where(p => p.OwnerId != me && !shown.Contains(p.Id))
                .OrderByDescending(p => p.Likes.Count).ThenByDescending(p => p.CreatedAt).Take(24 - popular.Count)
                .ToSummariesAsync(me, storage, ct));
        }

        var suggested = await MembersAsync(db.Users.Where(u => u.Id != me && u.SuspendedAt == null
            && !u.BlockedBy.Any(b => b.BlockerId == me)
            && !db.Follows.Any(f => f.FollowerId == me && f.FolloweeId == u.Id)
            && db.Photos.Any(p => p.OwnerId == u.Id)), me, 8, ct);

        return new ExploreDto(trending, popular, suggested);
    }

    private async Task<List<MemberResultDto>> MembersAsync(IQueryable<User> users, Guid me, int take, CancellationToken ct)
    {
        var visible = db.Photos.Where(Moderation.PhotoVisibleTo(me));
        return await users
            .Select(u => new
            {
                u.Id, u.DisplayName, u.AvatarUrl,
                Photos = visible.Count(p => p.OwnerId == u.Id),
                Followers = db.Follows.Count(f => f.FolloweeId == u.Id),
                Followed = db.Follows.Any(f => f.FollowerId == me && f.FolloweeId == u.Id),
            })
            .OrderByDescending(r => r.Followers).ThenByDescending(r => r.Photos).ThenBy(r => r.DisplayName)
            .Take(take)
            .Select(r => new MemberResultDto(new UserSummaryDto(r.Id, r.DisplayName, r.AvatarUrl), r.Photos, r.Followers, r.Followed))
            .ToListAsync(ct);
    }
}
