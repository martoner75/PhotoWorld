using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>A member's blog: its title and intro. Posts live in <see cref="BlogPostsController"/>.</summary>
[ApiController]
[Authorize]
public class BlogsController(PhotoWorldDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet(ApiRoutes.Blog)]
    public async Task<ActionResult<BlogDto>> Get(Guid userId, CancellationToken ct)
    {
        var me = User.GetUserId();
        var visiblePosts = db.BlogPosts.Where(Moderation.PostVisibleTo(me));
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                Author = new UserSummaryDto(u.Id, u.DisplayName, u.AvatarUrl),
                Blog = db.Blogs.Where(b => b.UserId == u.Id).Select(b => new { b.Title, b.Intro }).FirstOrDefault(),
                // The owner's count includes drafts; everyone else counts published posts they can see.
                Posts = visiblePosts.Count(p => p.AuthorId == u.Id && (u.Id == me || p.Status == BlogPostStatus.Published)),
            })
            .SingleOrDefaultAsync(ct);
        if (row is null) return NotFound();

        return new BlogDto(row.Author, row.Blog?.Title ?? DefaultTitle(row.Author.DisplayName), row.Blog?.Intro,
            row.Posts, userId == me);
    }

    [HttpPut(ApiRoutes.Blog)]
    public async Task<ActionResult<BlogDto>> Save(Guid userId, SaveBlogRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (userId != me) return Forbid();

        var now = clock.GetUtcNow();
        var blog = await db.Blogs.FindAsync([me], ct);
        if (blog is null)
        {
            blog = new Blog { UserId = me, Title = "", CreatedAt = now };
            db.Blogs.Add(blog);
        }
        blog.Title = request.Title.Trim();
        blog.Intro = string.IsNullOrWhiteSpace(request.Intro) ? null : request.Intro.Trim();
        blog.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return await Get(userId, ct);
    }

    internal static string DefaultTitle(string displayName) => $"{displayName}'s blog";
}
