using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Moderation;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

[ApiController]
[Authorize]
public class PhotosController(
    PhotoWorldDbContext db,
    ImageProcessor images,
    SupabaseStorage storage,
    ReportService reports,
    TimeProvider clock,
    ILogger<PhotosController> logger) : ControllerBase
{
    private const int PageSize = 24;

    /// <summary>Newest photos first, optionally only one member's. Hidden photos are left out except for their owner.</summary>
    [HttpGet(ApiRoutes.Photos)]
    public async Task<PagedResult<PhotoSummaryDto>> List(int page = 1, Guid? ownerId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var me = CurrentUserId();
        var commentVisible = Moderation.CommentVisibleTo(me);
        var query = VisiblePhotos(me);
        if (ownerId is not null) query = query.Where(p => p.OwnerId == ownerId);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(p => new
            {
                p.Id, p.Title, p.ThumbnailPath, p.Width, p.Height, p.CreatedAt,
                Owner = new UserSummaryDto(p.Owner.Id, p.Owner.DisplayName, p.Owner.AvatarUrl),
                Likes = p.Likes.Count,
                Comments = p.Comments.AsQueryable().Count(commentVisible),
                Upheld = p.Reports.Any(r => r.Status == ReportStatus.Upheld),
                OpenReports = p.Reports.Count(r => r.Status == ReportStatus.Open),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new PhotoSummaryDto(r.Id, r.Title, storage.PublicUrl(r.ThumbnailPath),
            r.Width, r.Height, r.Owner, r.Likes, r.Comments, r.CreatedAt,
            Moderation.State(r.Upheld, r.OpenReports))).ToList();
        return new PagedResult<PhotoSummaryDto>(items, page, PageSize, total);
    }

    [HttpGet(ApiRoutes.Photo)]
    public async Task<ActionResult<PhotoDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var detail = await LoadDetailAsync(id, ct);
        return detail is null ? NotFound() : detail;
    }

    [HttpPost(ApiRoutes.Photos)]
    [RequestSizeLimit(PhotoUploadLimits.MaxBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadLimits.MaxBytes + 1024 * 1024)]
    public async Task<ActionResult<PhotoDetailDto>> Upload(
        [FromForm] IFormFile? file, [FromForm] string? title, [FromForm] string? description,
        [FromForm] bool showOnHome, CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId || !await db.Users.AnyAsync(u => u.Id == userId, ct)) return Unauthorized();

        title = title?.Trim();
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (file is null) ModelState.AddModelError(nameof(file), "Choose a photo to upload.");
        if (string.IsNullOrEmpty(title)) ModelState.AddModelError(nameof(title), "Give your photo a title.");
        else if (title.Length > PhotoUploadLimits.MaxTitleLength) ModelState.AddModelError(nameof(title), "That title is too long.");
        if (description?.Length > PhotoUploadLimits.MaxDescriptionLength) ModelState.AddModelError(nameof(description), "That description is too long.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        ProcessedPhoto processed;
        try
        {
            using var buffer = new MemoryStream((int)file!.Length);
            await file.CopyToAsync(buffer, ct);
            processed = images.Process(buffer.ToArray());
        }
        catch (ImageValidationException ex)
        {
            ModelState.AddModelError(nameof(file), ex.Message);
            return ValidationProblem(ModelState);
        }

        var now = clock.GetUtcNow();
        var photo = new Photo
        {
            OwnerId = userId,
            Title = title!,
            Description = description,
            DisplayPath = "",
            ThumbnailPath = "",
            Width = processed.Display.Width,
            Height = processed.Display.Height,
            SizeBytes = processed.Display.Bytes.Length,
            CameraMake = processed.Camera.Make,
            CameraModel = processed.Camera.Model,
            Lens = processed.Camera.Lens,
            FocalLengthMm = processed.Camera.FocalLengthMm,
            Aperture = processed.Camera.Aperture,
            ExposureTime = processed.Camera.ExposureTime,
            Iso = processed.Camera.Iso,
            TakenAt = processed.Camera.TakenAt,
            ShowOnHome = showOnHome,
            CreatedAt = now,
            UpdatedAt = now,
        };
        photo.DisplayPath = $"{userId}/{photo.Id}/display.jpg";
        photo.ThumbnailPath = $"{userId}/{photo.Id}/thumb.jpg";

        // Files first, then the row: a row never points at missing files. If saving fails, clean the files up.
        await storage.UploadAsync(photo.DisplayPath, processed.Display.Bytes, "image/jpeg", ct);
        await storage.UploadAsync(photo.ThumbnailPath, processed.Thumbnail.Bytes, "image/jpeg", ct);
        try
        {
            db.Photos.Add(photo);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await TryDeleteFilesAsync(photo);
            throw;
        }

        logger.LogInformation("User {UserId} uploaded photo {PhotoId} ({Width}x{Height}, {Bytes} bytes)",
            userId, photo.Id, photo.Width, photo.Height, photo.SizeBytes);
        return CreatedAtAction(nameof(Get), new { id = photo.Id }, await LoadDetailAsync(photo.Id, ct));
    }

    [HttpPut(ApiRoutes.Photo)]
    public async Task<ActionResult<PhotoDetailDto>> Update(Guid id, UpdatePhotoRequest request, CancellationToken ct)
    {
        var photo = await db.Photos.FindAsync([id], ct);
        if (photo is null) return NotFound();
        if (photo.OwnerId != CurrentUserId()) return Forbid();

        photo.Title = request.Title.Trim();
        photo.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        photo.ShowOnHome = request.ShowOnHome;
        photo.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} edited photo {PhotoId}", photo.OwnerId, id);
        return (await LoadDetailAsync(id, ct))!;
    }

    [HttpDelete(ApiRoutes.Photo)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var photo = await db.Photos.FindAsync([id], ct);
        if (photo is null) return NotFound();
        if (photo.OwnerId != CurrentUserId()) return Forbid();

        db.Photos.Remove(photo);
        await db.SaveChangesAsync(ct);
        await TryDeleteFilesAsync(photo);

        logger.LogInformation("User {UserId} deleted photo {PhotoId}", photo.OwnerId, id);
        return NoContent();
    }

    [HttpPost(ApiRoutes.PhotoLike)]
    public async Task<ActionResult<LikeStateDto>> Like(Guid id, CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        if (!await VisiblePhotos(userId).AnyAsync(p => p.Id == id, ct)) return NotFound();

        if (!await db.PhotoLikes.AnyAsync(l => l.PhotoId == id && l.UserId == userId, ct))
        {
            db.PhotoLikes.Add(new PhotoLike { PhotoId = id, UserId = userId, CreatedAt = clock.GetUtcNow() });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { /* Liked twice at the same moment: the like already exists. */ }
        }
        return await LikeStateAsync(id, userId, ct);
    }

    [HttpDelete(ApiRoutes.PhotoLike)]
    public async Task<ActionResult<LikeStateDto>> Unlike(Guid id, CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        if (!await VisiblePhotos(userId).AnyAsync(p => p.Id == id, ct)) return NotFound();

        await db.PhotoLikes.Where(l => l.PhotoId == id && l.UserId == userId).ExecuteDeleteAsync(ct);
        return await LikeStateAsync(id, userId, ct);
    }

    [HttpGet(ApiRoutes.PhotoComments)]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> Comments(Guid id, CancellationToken ct)
    {
        var me = CurrentUserId();
        var ownerId = await VisiblePhotos(me).Where(p => p.Id == id).Select(p => (Guid?)p.OwnerId).SingleOrDefaultAsync(ct);
        if (ownerId is null) return NotFound();

        var rows = await db.PhotoComments.AsNoTracking()
            .Where(c => c.PhotoId == id)
            .Where(Moderation.CommentVisibleTo(me))
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

        return rows.Select(c => new CommentDto(c.Id, c.Author, c.Text, c.CreatedAt,
            CanDelete: c.AuthorId == me || ownerId == me,
            Moderation: Moderation.State(c.Upheld, c.OpenReports),
            CanReport: c.AuthorId != me && !c.ReportedByMe)).ToList();
    }

    [HttpPost(ApiRoutes.PhotoComments)]
    public async Task<ActionResult<CommentDto>> AddComment(Guid id, CreateCommentRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        if (!await VisiblePhotos(userId).AnyAsync(p => p.Id == id, ct)) return NotFound();

        var text = request.Text.Trim();
        if (text.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Text), "Write something first.");
            return ValidationProblem(ModelState);
        }

        var comment = new PhotoComment { PhotoId = id, AuthorId = userId, Text = text, CreatedAt = clock.GetUtcNow() };
        db.PhotoComments.Add(comment);
        await db.SaveChangesAsync(ct);

        var author = await db.Users.Where(u => u.Id == userId)
            .Select(u => new UserSummaryDto(u.Id, u.DisplayName, u.AvatarUrl)).SingleAsync(ct);
        return new CommentDto(comment.Id, author, comment.Text, comment.CreatedAt, CanDelete: true);
    }

    [HttpDelete(ApiRoutes.PhotoComment)]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken ct)
    {
        var comment = await db.PhotoComments.Include(c => c.Photo)
            .SingleOrDefaultAsync(c => c.Id == commentId && c.PhotoId == id, ct);
        if (comment is null) return NotFound();

        // Authors can remove their own comments; photo owners can remove any comment on their photo.
        var me = CurrentUserId();
        if (comment.AuthorId != me && comment.Photo.OwnerId != me) return Forbid();

        db.PhotoComments.Remove(comment);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost(ApiRoutes.PhotoReports)]
    public async Task<IActionResult> ReportPhoto(Guid id, CreateReportRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } me) return Unauthorized();
        var ownerId = await VisiblePhotos(me).Where(p => p.Id == id).Select(p => (Guid?)p.OwnerId).SingleOrDefaultAsync(ct);
        if (ownerId is null) return NotFound();
        if (ownerId == me) return Problem("You can't report your own photo.", statusCode: StatusCodes.Status400BadRequest);

        return this.ToResponse(await reports.SaveAsync(ReportTarget.Photo(id), me, request, ct));
    }

    [HttpPost(ApiRoutes.CommentReports)]
    public async Task<IActionResult> ReportComment(Guid id, Guid commentId, CreateReportRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } me) return Unauthorized();
        if (!await VisiblePhotos(me).AnyAsync(p => p.Id == id, ct)) return NotFound();
        var authorId = await db.PhotoComments
            .Where(c => c.Id == commentId && c.PhotoId == id)
            .Where(Moderation.CommentVisibleTo(me))
            .Select(c => (Guid?)c.AuthorId).SingleOrDefaultAsync(ct);
        if (authorId is null) return NotFound();
        if (authorId == me) return Problem("You can't report your own comment.", statusCode: StatusCodes.Status400BadRequest);

        return this.ToResponse(await reports.SaveAsync(ReportTarget.PhotoComment(commentId), me, request, ct));
    }

    private Guid? CurrentUserId() => User.GetUserId();

    /// <summary>Photos this user may see: everything that isn't hidden by moderation, plus their own.</summary>
    private IQueryable<Photo> VisiblePhotos(Guid? userId) =>
        db.Photos.AsNoTracking().Where(Moderation.PhotoVisibleTo(userId));

    private async Task<PhotoDetailDto?> LoadDetailAsync(Guid id, CancellationToken ct)
    {
        var me = CurrentUserId();
        var commentVisible = Moderation.CommentVisibleTo(me);
        var row = await VisiblePhotos(me)
            .Where(p => p.Id == id)
            .Select(p => new
            {
                Photo = p,
                Owner = new UserSummaryDto(p.Owner.Id, p.Owner.DisplayName, p.Owner.AvatarUrl),
                Likes = p.Likes.Count,
                Comments = p.Comments.AsQueryable().Count(commentVisible),
                LikedByMe = p.Likes.Any(l => l.UserId == me),
                Upheld = p.Reports.Any(r => r.Status == ReportStatus.Upheld),
                OpenReports = p.Reports.Count(r => r.Status == ReportStatus.Open),
                ReportedByMe = p.Reports.Any(r => r.ReporterId == me),
            })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;

        var p = row.Photo;
        return new PhotoDetailDto(p.Id, p.Title, p.Description,
            storage.PublicUrl(p.DisplayPath), storage.PublicUrl(p.ThumbnailPath), p.Width, p.Height, row.Owner,
            new CameraDetailsDto(p.CameraMake, p.CameraModel, p.Lens, p.FocalLengthMm, p.Aperture, p.ExposureTime, p.Iso, p.TakenAt),
            row.Likes, row.Comments, row.LikedByMe, p.OwnerId == me, p.CreatedAt, p.UpdatedAt,
            Moderation.State(row.Upheld, row.OpenReports),
            CanReport: p.OwnerId != me && !row.ReportedByMe,
            ShowOnHome: p.ShowOnHome);
    }

    private async Task<LikeStateDto> LikeStateAsync(Guid photoId, Guid userId, CancellationToken ct) => new(
        await db.PhotoLikes.AnyAsync(l => l.PhotoId == photoId && l.UserId == userId, ct),
        await db.PhotoLikes.CountAsync(l => l.PhotoId == photoId, ct));

    private async Task TryDeleteFilesAsync(Photo photo)
    {
        try
        {
            await storage.DeleteAsync([photo.DisplayPath, photo.ThumbnailPath], CancellationToken.None);
        }
        catch (HttpRequestException ex)
        {
            // The database is the source of truth; an orphaned file is harmless but worth knowing about.
            logger.LogWarning(ex, "Could not delete storage files for photo {PhotoId}", photo.Id);
        }
    }
}
