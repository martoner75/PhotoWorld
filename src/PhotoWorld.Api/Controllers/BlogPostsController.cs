using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Blogs;
using PhotoWorld.Api.Services.Moderation;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

[ApiController]
[Authorize]
public class BlogPostsController(
    PhotoWorldDbContext db,
    BlogHtml html,
    SupabaseStorage storage,
    ReportService reports,
    TimeProvider clock,
    ILogger<BlogPostsController> logger) : ControllerBase
{
    private const int PageSize = 12;

    /// <summary>
    /// Newest posts first. Without <paramref name="authorId"/>: everyone's published posts. With it: that
    /// member's published posts, plus their drafts when it's the caller's own blog.
    /// </summary>
    [HttpGet(ApiRoutes.BlogPosts)]
    public async Task<PagedResult<BlogPostSummaryDto>> List(int page = 1, Guid? authorId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var me = CurrentUserId();
        var commentVisible = Moderation.BlogCommentVisibleTo(me);
        var visiblePhotos = db.Photos.Where(Moderation.PhotoVisibleTo(me));

        var query = db.BlogPosts.AsNoTracking().Where(Moderation.PostVisibleTo(me));
        query = authorId is null
            ? query.Where(p => p.Status == BlogPostStatus.Published)
            : query.Where(p => p.AuthorId == authorId && (p.AuthorId == me || p.Status == BlogPostStatus.Published));

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(p => p.PublishedAt ?? p.UpdatedAt)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(p => new
            {
                p.Id, p.Title, p.Excerpt, p.Status, p.PublishedAt, p.UpdatedAt,
                Author = new UserSummaryDto(p.Author.Id, p.Author.DisplayName, p.Author.AvatarUrl),
                CoverPath = visiblePhotos.Where(ph => ph.Id == p.CoverPhotoId).Select(ph => ph.ThumbnailPath).FirstOrDefault(),
                Comments = p.Comments.AsQueryable().Count(commentVisible),
                Upheld = p.Reports.Any(r => r.Status == ReportStatus.Upheld),
                OpenReports = p.Reports.Count(r => r.Status == ReportStatus.Open),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new BlogPostSummaryDto(r.Id, r.Title, r.Excerpt,
            r.CoverPath is null ? null : storage.PublicUrl(r.CoverPath), r.Author, r.Comments, r.Status,
            r.PublishedAt, r.UpdatedAt, Moderation.State(r.Upheld, r.OpenReports))).ToList();
        return new PagedResult<BlogPostSummaryDto>(items, page, PageSize, total);
    }

    [HttpGet(ApiRoutes.BlogPost)]
    public async Task<ActionResult<BlogPostDto>> Get(Guid id, CancellationToken ct)
    {
        var post = await LoadAsync(id, ct);
        return post is null ? NotFound() : post;
    }

    [HttpPost(ApiRoutes.BlogPosts)]
    public async Task<ActionResult<BlogPostDto>> Create(SavePostRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } me || !await db.Users.AnyAsync(u => u.Id == me, ct)) return Unauthorized();

        var cleaned = await CleanAsync(request, me, ct);
        if (cleaned is null) return ValidationProblem(ModelState);

        var now = clock.GetUtcNow();
        var post = new BlogPost
        {
            AuthorId = me,
            Title = request.Title.Trim(),
            BodyHtml = cleaned.Html,
            Excerpt = cleaned.Excerpt,
            CoverPhotoId = cleaned.PhotoIds.Count > 0 ? cleaned.PhotoIds[0] : null,
            Status = request.Publish ? BlogPostStatus.Published : BlogPostStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = request.Publish ? now : null,
            Photos = cleaned.PhotoIds.Select(photoId => new BlogPostPhoto { PhotoId = photoId }).ToList(),
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} created blog post {PostId} ({Status}, {PhotoCount} photos)",
            me, post.Id, post.Status, cleaned.PhotoIds.Count);
        return CreatedAtAction(nameof(Get), new { id = post.Id }, await LoadAsync(post.Id, ct));
    }

    [HttpPut(ApiRoutes.BlogPost)]
    public async Task<ActionResult<BlogPostDto>> Update(Guid id, SavePostRequest request, CancellationToken ct)
    {
        var post = await db.BlogPosts.Include(p => p.Photos).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return NotFound();
        if (CurrentUserId() is not { } me || post.AuthorId != me) return Forbid();

        var cleaned = await CleanAsync(request, me, ct);
        if (cleaned is null) return ValidationProblem(ModelState);

        var now = clock.GetUtcNow();
        post.Title = request.Title.Trim();
        post.BodyHtml = cleaned.Html;
        post.Excerpt = cleaned.Excerpt;
        post.CoverPhotoId = cleaned.PhotoIds.Count > 0 ? cleaned.PhotoIds[0] : null;
        post.Status = request.Publish ? BlogPostStatus.Published : BlogPostStatus.Draft;
        post.PublishedAt = request.Publish ? post.PublishedAt ?? now : null;
        post.UpdatedAt = now;
        post.Photos.RemoveAll(p => !cleaned.PhotoIds.Contains(p.PhotoId));
        foreach (var photoId in cleaned.PhotoIds.Where(pid => post.Photos.All(p => p.PhotoId != pid)))
            post.Photos.Add(new BlogPostPhoto { PostId = post.Id, PhotoId = photoId });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} edited blog post {PostId} ({Status})", me, id, post.Status);
        return (await LoadAsync(id, ct))!;
    }

    [HttpDelete(ApiRoutes.BlogPost)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var post = await db.BlogPosts.FindAsync([id], ct);
        if (post is null) return NotFound();
        if (post.AuthorId != CurrentUserId()) return Forbid();

        db.BlogPosts.Remove(post);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} deleted blog post {PostId}", post.AuthorId, id);
        return NoContent();
    }

    [HttpGet(ApiRoutes.BlogPostComments)]
    public async Task<ActionResult<IReadOnlyList<BlogCommentDto>>> Comments(Guid id, CancellationToken ct)
    {
        var me = CurrentUserId();
        var authorId = await VisiblePosts(me).Where(p => p.Id == id).Select(p => (Guid?)p.AuthorId).SingleOrDefaultAsync(ct);
        if (authorId is null) return NotFound();

        var rows = await db.BlogComments.AsNoTracking()
            .Where(c => c.PostId == id)
            .Where(Moderation.BlogCommentVisibleTo(me))
            .OrderBy(c => c.CreatedAt)
            .Take(500)
            .Select(c => new
            {
                c.Id, c.AuthorId, c.Text, c.CreatedAt,
                Author = new UserSummaryDto(c.Author.Id, c.Author.DisplayName, c.Author.AvatarUrl),
                Upheld = c.Reports.Any(r => r.Status == ReportStatus.Upheld),
                OpenReports = c.Reports.Count(r => r.Status == ReportStatus.Open),
                ReportedByMe = c.Reports.Any(r => r.ReporterId == me),
            })
            .ToListAsync(ct);

        return rows.Select(c => new BlogCommentDto(c.Id, c.Author, c.Text, c.CreatedAt,
            CanDelete: c.AuthorId == me || authorId == me,
            Moderation: Moderation.State(c.Upheld, c.OpenReports),
            CanReport: c.AuthorId != me && !c.ReportedByMe)).ToList();
    }

    [HttpPost(ApiRoutes.BlogPostComments)]
    public async Task<ActionResult<BlogCommentDto>> AddComment(Guid id, CreateBlogCommentRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } me) return Unauthorized();
        // Comments only on published posts (an author previewing a draft can't comment on it yet).
        if (!await VisiblePosts(me).AnyAsync(p => p.Id == id && p.Status == BlogPostStatus.Published, ct)) return NotFound();

        var text = request.Text.Trim();
        if (text.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Text), "Write something first.");
            return ValidationProblem(ModelState);
        }

        var comment = new BlogComment { PostId = id, AuthorId = me, Text = text, CreatedAt = clock.GetUtcNow() };
        db.BlogComments.Add(comment);
        await db.SaveChangesAsync(ct);

        var author = await db.Users.Where(u => u.Id == me)
            .Select(u => new UserSummaryDto(u.Id, u.DisplayName, u.AvatarUrl)).SingleAsync(ct);
        return new BlogCommentDto(comment.Id, author, comment.Text, comment.CreatedAt, CanDelete: true,
            ModerationState.Visible, CanReport: false);
    }

    [HttpDelete(ApiRoutes.BlogPostComment)]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken ct)
    {
        var comment = await db.BlogComments.Include(c => c.Post)
            .SingleOrDefaultAsync(c => c.Id == commentId && c.PostId == id, ct);
        if (comment is null) return NotFound();

        // Comment authors can remove their comments; post authors can remove any comment on their post.
        var me = CurrentUserId();
        if (comment.AuthorId != me && comment.Post.AuthorId != me) return Forbid();

        db.BlogComments.Remove(comment);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost(ApiRoutes.BlogPostReports)]
    public async Task<IActionResult> ReportPost(Guid id, CreateReportRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } me) return Unauthorized();
        var authorId = await VisiblePosts(me).Where(p => p.Id == id).Select(p => (Guid?)p.AuthorId).SingleOrDefaultAsync(ct);
        if (authorId is null) return NotFound();
        if (authorId == me) return Problem("You can't report your own post.", statusCode: StatusCodes.Status400BadRequest);

        return this.ToResponse(await reports.SaveAsync(ReportTarget.BlogPost(id), me, request, ct));
    }

    [HttpPost(ApiRoutes.BlogCommentReports)]
    public async Task<IActionResult> ReportComment(Guid id, Guid commentId, CreateReportRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } me) return Unauthorized();
        if (!await VisiblePosts(me).AnyAsync(p => p.Id == id, ct)) return NotFound();
        var authorId = await db.BlogComments
            .Where(c => c.Id == commentId && c.PostId == id)
            .Where(Moderation.BlogCommentVisibleTo(me))
            .Select(c => (Guid?)c.AuthorId).SingleOrDefaultAsync(ct);
        if (authorId is null) return NotFound();
        if (authorId == me) return Problem("You can't report your own comment.", statusCode: StatusCodes.Status400BadRequest);

        return this.ToResponse(await reports.SaveAsync(ReportTarget.BlogComment(commentId), me, request, ct));
    }

    private Guid? CurrentUserId() => User.GetUserId();

    private IQueryable<BlogPost> VisiblePosts(Guid? userId) =>
        db.BlogPosts.AsNoTracking().Where(Moderation.PostVisibleTo(userId));

    /// <summary>Sanitizes the body, keeping only the author's own photos. Null (with model errors) if invalid.</summary>
    private async Task<CleanedPost?> CleanAsync(SavePostRequest request, Guid authorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) ModelState.AddModelError(nameof(request.Title), "Give your post a title.");

        var referenced = html.ReferencedPhotoIds(request.BodyHtml);
        var ownPhotos = (await db.Photos
                .Where(p => p.OwnerId == authorId && referenced.Contains(p.Id))
                .Select(p => new { p.Id, p.DisplayPath })
                .ToListAsync(ct))
            .ToDictionary(p => p.Id, p => storage.PublicUrl(p.DisplayPath));

        var cleaned = html.Clean(request.BodyHtml, ownPhotos);
        if (cleaned.IsEmpty) ModelState.AddModelError(nameof(request.BodyHtml), "Write something or add a photo first.");
        if (cleaned.Html.Length > BlogLimits.BodyMax) ModelState.AddModelError(nameof(request.BodyHtml), "That post is too long.");
        return ModelState.IsValid ? cleaned : null;
    }

    private async Task<BlogPostDto?> LoadAsync(Guid id, CancellationToken ct)
    {
        var me = CurrentUserId();
        var commentVisible = Moderation.BlogCommentVisibleTo(me);
        var visiblePhotos = db.Photos.Where(Moderation.PhotoVisibleTo(me));
        var row = await VisiblePosts(me)
            .Where(p => p.Id == id)
            .Select(p => new
            {
                Post = p,
                Author = new UserSummaryDto(p.Author.Id, p.Author.DisplayName, p.Author.AvatarUrl),
                CoverPath = visiblePhotos.Where(ph => ph.Id == p.CoverPhotoId).Select(ph => ph.DisplayPath).FirstOrDefault(),
                // Embedded photos the reader can still see; deleted or hidden ones are dropped from the body.
                PhotoIds = p.Photos.Select(x => x.PhotoId).Where(pid => visiblePhotos.Any(ph => ph.Id == pid)).ToList(),
                Comments = p.Comments.AsQueryable().Count(commentVisible),
                Upheld = p.Reports.Any(r => r.Status == ReportStatus.Upheld),
                OpenReports = p.Reports.Count(r => r.Status == ReportStatus.Open),
                ReportedByMe = p.Reports.Any(r => r.ReporterId == me),
            })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;

        var post = row.Post;
        return new BlogPostDto(post.Id, post.Title, html.WithoutPhotosExcept(post.BodyHtml, row.PhotoIds.ToHashSet()),
            row.CoverPath is null ? null : storage.PublicUrl(row.CoverPath), row.Author, post.Status,
            post.PublishedAt, post.CreatedAt, post.UpdatedAt, row.Comments,
            CanEdit: post.AuthorId == me,
            CanReport: post.AuthorId != me && !row.ReportedByMe,
            Moderation.State(row.Upheld, row.OpenReports));
    }
}
