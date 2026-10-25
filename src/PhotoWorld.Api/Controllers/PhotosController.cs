using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Api.Services.Moderation;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

[ApiController]
[Authorize]
public class PhotosController(
    PhotoWorldDbContext db,
    ImageProcessor images,
    SupabaseStorage storage,
    ReportService reports,
    ActivityFeed activity,
    AdminAccess admins,
    TimeProvider clock,
    ILogger<PhotosController> logger) : ControllerBase
{
    private const int PageSize = 24;

    /// <summary>
    /// Newest photos first: optionally only one member's, only from members the caller follows, and/or matching
    /// camera settings. Hidden photos are left out except for their owner.
    /// </summary>
    [HttpGet(ApiRoutes.Photos)]
    public async Task<PagedResult<PhotoSummaryDto>> List(int page = 1, Guid? ownerId = null, bool following = false,
        [FromQuery] PhotoSearch? search = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var me = CurrentUserId();
        var query = VisiblePhotos(me);
        if (ownerId is not null) query = query.Where(p => p.OwnerId == ownerId);
        if (following) query = query.Where(p => db.Follows.Any(f => f.FollowerId == me && f.FolloweeId == p.OwnerId));
        if (search is not null) query = query.Matching(search);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToSummariesAsync(me, storage, ct);
        return new PagedResult<PhotoSummaryDto>(items, page, PageSize, total);
    }

    /// <summary>Cameras and lenses in the gallery, most used first, as suggestions for the filter form.</summary>
    [HttpGet(ApiRoutes.PhotoCameraOptions)]
    public async Task<CameraOptionsDto> CameraOptions(CancellationToken ct)
    {
        const int max = 100;
        var photos = VisiblePhotos(CurrentUserId());
        var cameras = await photos.Where(p => p.CameraModel != null)
            .GroupBy(p => p.CameraModel!).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => g.Key).Take(max).ToListAsync(ct);
        var lenses = await photos.Where(p => p.Lens != null)
            .GroupBy(p => p.Lens!).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => g.Key).Take(max).ToListAsync(ct);
        return new CameraOptionsDto(cameras, lenses);
    }

    [HttpGet(ApiRoutes.Photo)]
    public async Task<ActionResult<PhotoDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var detail = await LoadDetailAsync(id, ct);
        if (detail is null) return NotFound();
        if (!detail.CanEdit) await CountViewAsync(id, ct);
        return detail;
    }

    /// <summary>Adds one to today's views (a single upsert). A failure never stops the photo from showing.</summary>
    private async Task CountViewAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                insert into photoworld.photo_view_days (photo_id, day, views) values ({id}, {day}, 1)
                on conflict (photo_id, day) do update set views = photo_view_days.views + 1", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not count a view of photo {PhotoId}", id);
        }
    }

    [HttpPost(ApiRoutes.Photos)]
    [RequestSizeLimit(PhotoUploadLimits.MaxBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadLimits.MaxBytes + 1024 * 1024)]
    public async Task<ActionResult<PhotoDetailDto>> Upload(
        [FromForm] IFormFile? file, [FromForm] string? title, [FromForm] string? description,
        [FromForm] bool showOnHome, [FromForm] Guid? placeId, [FromForm] string? tags, [FromForm] bool wantsFeedback,
        [FromForm] bool watermark, [FromForm] bool? allowDownload, CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId || !await db.Users.AnyAsync(u => u.Id == userId, ct)) return Unauthorized();

        title = title?.Trim();
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (file is null) ModelState.AddModelError(nameof(file), "Choose a photo to upload.");
        if (string.IsNullOrEmpty(title)) ModelState.AddModelError(nameof(title), "Give your photo a title.");
        else if (title.Length > PhotoUploadLimits.MaxTitleLength) ModelState.AddModelError(nameof(title), "That title is too long.");
        if (description?.Length > PhotoUploadLimits.MaxDescriptionLength) ModelState.AddModelError(nameof(description), "That description is too long.");
        if (!await IsOwnPlaceAsync(placeId, userId, ct)) ModelState.AddModelError(nameof(placeId), "Choose one of your own places.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        ProcessedPhoto processed;
        try
        {
            using var buffer = new MemoryStream((int)file!.Length);
            await file.CopyToAsync(buffer, ct);
            var mark = watermark ? $"© {await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).SingleAsync(ct)}" : null;
            processed = images.Process(buffer.ToArray(), mark);
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
            PlaceId = placeId,
            WantsFeedback = wantsFeedback,
            Watermarked = watermark,
            AllowDownload = allowDownload ?? true,
            Tags = PhotoTags.Parse(tags, description).Select(name => new PhotoTag { Name = name }).ToList(),
            CreatedAt = now,
            UpdatedAt = now,
            ReviewStatus = PhotoReview.StatusOnUpload(await admins.IsAdminAsync(userId, ct)),
        };
        photo.DisplayPath = $"{userId}/{photo.Id}/display.jpg";
        // With image transforms on, Supabase resizes the display image for thumbnails, so no thumbnail file is stored.
        // The path still points at a real file, so turning transforms off later shows the display image instead.
        photo.ThumbnailPath = storage.StoresThumbnails ? $"{userId}/{photo.Id}/thumb.jpg" : photo.DisplayPath;

        // Files first, then the row: a row never points at missing files. If saving fails, clean the files up.
        await storage.UploadAsync(photo.DisplayPath, processed.Display.Bytes, "image/jpeg", ct);
        if (storage.StoresThumbnails)
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

        logger.LogInformation("User {UserId} uploaded photo {PhotoId} ({Width}x{Height}, {Bytes} bytes, {Review})",
            userId, photo.Id, photo.Width, photo.Height, photo.SizeBytes, photo.ReviewStatus);
        // Followers hear about a photo once others can see it: now for admins, otherwise when it's approved.
        if (photo.ReviewStatus == PhotoReviewStatus.Approved)
            await activity.NotifyFollowersAsync(userId, ActivityTypes.NewPhoto, photoId: photo.Id, ct: ct);
        return CreatedAtAction(nameof(Get), new { id = photo.Id }, await LoadDetailAsync(photo.Id, ct));
    }

    /// <summary>
    /// Admins: photos waiting for approval, longest-waiting first. Photos in a blog post that's waiting for approval
    /// are left out (approving the post approves them), and so are suspended members' photos.
    /// </summary>
    [HttpGet(ApiRoutes.AdminPendingPhotos)]
    public async Task<ActionResult<PagedResult<PhotoSummaryDto>>> Pending(int page = 1, CancellationToken ct = default)
    {
        var me = CurrentUserId();
        if (!await admins.IsAdminAsync(me, ct)) return Forbid();
        page = Math.Max(1, page);
        var query = db.Photos.AsNoTracking()
            .Where(p => p.ReviewStatus == PhotoReviewStatus.Pending && p.Owner.SuspendedAt == null)
            .Where(p => !db.BlogPosts.Any(post => post.Status == Shared.Blogs.BlogPostStatus.Pending
                && (post.CoverPhotoId == p.Id || post.Photos.Any(x => x.PhotoId == p.Id))));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.CreatedAt)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToSummariesAsync(me, storage, ct);
        return new PagedResult<PhotoSummaryDto>(items, page, PageSize, total);
    }

    /// <summary>Admins: approves pending photos (others in the list are skipped). Owners and their followers are told.</summary>
    [HttpPost(ApiRoutes.AdminPhotoApproval)]
    public async Task<ActionResult<int>> Approve(ApprovePhotosRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } me || !await admins.IsAdminAsync(me, ct)) return Forbid();
        var ids = request.PhotoIds.Distinct().Take(PageSize * 2).ToList();
        var photos = await db.Photos.Where(p => ids.Contains(p.Id) && p.ReviewStatus == PhotoReviewStatus.Pending).ToListAsync(ct);

        var now = clock.GetUtcNow();
        foreach (var photo in photos)
        {
            photo.ReviewStatus = PhotoReviewStatus.Approved;
            photo.ReviewedAt = now;
            photo.ReviewNote = null;
        }
        await db.SaveChangesAsync(ct);

        foreach (var photo in photos)
        {
            logger.LogInformation("Admin {AdminId} approved photo {PhotoId} by {OwnerId}", me, photo.Id, photo.OwnerId);
            await activity.NotifyAsync(photo.OwnerId, me, ActivityTypes.PhotoApproved, photoId: photo.Id, ct: ct);
            await activity.NotifyFollowersAsync(photo.OwnerId, ActivityTypes.NewPhoto, photoId: photo.Id, ct: ct);
        }
        return photos.Count;
    }

    /// <summary>Admins: refuses a pending photo with an optional reason. It stays visible to its owner only.</summary>
    [HttpPost(ApiRoutes.AdminPhotoRejection)]
    public async Task<IActionResult> Reject(Guid id, RejectPhotoRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } me || !await admins.IsAdminAsync(me, ct)) return Forbid();
        var photo = await db.Photos.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (photo is null) return NotFound();
        if (!PhotoReview.CanReview(photo.ReviewStatus))
            return Problem("This photo isn't waiting for approval any more.", statusCode: StatusCodes.Status409Conflict);

        photo.ReviewStatus = PhotoReviewStatus.Rejected;
        photo.ReviewedAt = clock.GetUtcNow();
        photo.ReviewNote = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Admin {AdminId} rejected photo {PhotoId} by {OwnerId}", me, id, photo.OwnerId);
        await activity.NotifyAsync(photo.OwnerId, me, ActivityTypes.PhotoRejected, photoId: id, snippet: photo.ReviewNote, ct: ct);
        return NoContent();
    }

    [HttpPut(ApiRoutes.Photo)]
    public async Task<ActionResult<PhotoDetailDto>> Update(Guid id, UpdatePhotoRequest request, CancellationToken ct)
    {
        var photo = await db.Photos.Include(p => p.Tags).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (photo is null) return NotFound();
        if (photo.OwnerId != CurrentUserId()) return Forbid();
        if (!await IsOwnPlaceAsync(request.PlaceId, photo.OwnerId, ct))
        {
            ModelState.AddModelError(nameof(request.PlaceId), "Choose one of your own places.");
            return ValidationProblem(ModelState);
        }

        photo.Title = request.Title.Trim();
        photo.PlaceId = request.PlaceId;
        photo.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        photo.ShowOnHome = request.ShowOnHome;
        if (request.WantsFeedback is { } wants) photo.WantsFeedback = wants;
        if (request.AllowDownload is { } allow) photo.AllowDownload = allow;
        // Clients that don't send tags (older apps) leave them alone; #hashtags in the description always count.
        var tagNames = PhotoTags.Parse(request.Tags ?? PhotoTags.Join(photo.Tags.Select(t => t.Name)), photo.Description);
        photo.Tags.RemoveAll(t => !tagNames.Contains(t.Name));
        foreach (var name in tagNames.Where(n => photo.Tags.All(t => t.Name != n))) photo.Tags.Add(new PhotoTag { PhotoId = id, Name = name });
        photo.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} edited photo {PhotoId}", photo.OwnerId, id);
        return (await LoadDetailAsync(id, ct))!;
    }

    /// <summary>
    /// Adds or replaces the photo's unedited "before" version, shown with a before/after slider. Processed like an
    /// upload (metadata stripped, the photo's watermark if it has one). A member's approved photo goes back for approval.
    /// </summary>
    [HttpPost(ApiRoutes.PhotoBefore)]
    [RequestSizeLimit(PhotoUploadLimits.MaxBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadLimits.MaxBytes + 1024 * 1024)]
    public async Task<ActionResult<PhotoDetailDto>> AddBefore(Guid id, [FromForm] IFormFile? file, CancellationToken ct)
    {
        var photo = await db.Photos.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (photo is null) return NotFound();
        if (CurrentUserId() is not { } me || photo.OwnerId != me) return Forbid();
        if (photo.ReviewStatus == PhotoReviewStatus.Rejected)
            return Problem("This photo wasn't approved, so it can't have a before version.", statusCode: StatusCodes.Status409Conflict);
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "Choose the unedited photo.");
            return ValidationProblem(ModelState);
        }

        ProcessedPhoto processed;
        try
        {
            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, ct);
            var mark = photo.Watermarked ? $"© {await db.Users.Where(u => u.Id == me).Select(u => u.DisplayName).SingleAsync(ct)}" : null;
            processed = images.Process(buffer.ToArray(), mark);
        }
        catch (ImageValidationException ex)
        {
            ModelState.AddModelError(nameof(file), ex.Message);
            return ValidationProblem(ModelState);
        }

        // A new name each time, so browsers and the storage CDN never show a replaced image.
        var old = photo.BeforeDisplayPath;
        var path = $"{me}/{id}/before-{Guid.NewGuid():N}.jpg";
        await storage.UploadAsync(path, processed.Display.Bytes, "image/jpeg", ct);
        photo.BeforeDisplayPath = path;
        var wasApproved = photo.ReviewStatus == PhotoReviewStatus.Approved;
        photo.ReviewStatus = PhotoReview.StatusAfterNewImage(photo.ReviewStatus, await admins.IsAdminAsync(me, ct));
        photo.UpdatedAt = clock.GetUtcNow();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await TryDeleteAsync([path], id);
            throw;
        }
        if (old is not null) await TryDeleteAsync([old], id);

        logger.LogInformation("User {UserId} added a before version to photo {PhotoId}{Review}", me, id,
            wasApproved && photo.ReviewStatus == PhotoReviewStatus.Pending ? " (back for approval)" : "");
        return (await LoadDetailAsync(id, ct))!;
    }

    [HttpDelete(ApiRoutes.PhotoBefore)]
    public async Task<IActionResult> RemoveBefore(Guid id, CancellationToken ct)
    {
        var photo = await db.Photos.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (photo is null) return NotFound();
        if (photo.OwnerId != CurrentUserId()) return Forbid();
        if (photo.BeforeDisplayPath is not { } path) return NoContent();

        photo.BeforeDisplayPath = null;
        photo.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await TryDeleteAsync([path], id);
        logger.LogInformation("User {UserId} removed the before version of photo {PhotoId}", photo.OwnerId, id);
        return NoContent();
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
        var ownerId = await VisiblePhotos(userId).Where(p => p.Id == id).Select(p => (Guid?)p.OwnerId).SingleOrDefaultAsync(ct);
        if (ownerId is null) return NotFound();

        if (!await db.PhotoLikes.AnyAsync(l => l.PhotoId == id && l.UserId == userId, ct))
        {
            db.PhotoLikes.Add(new PhotoLike { PhotoId = id, UserId = userId, CreatedAt = clock.GetUtcNow() });
            try
            {
                await db.SaveChangesAsync(ct);
                await activity.NotifyAsync(ownerId.Value, userId, ActivityTypes.Like, photoId: id, ct: ct);
            }
            catch (DbUpdateException) { /* Liked twice at the same moment: the like already exists. */ }
        }
        return await LikeStateAsync(id, userId, ct);
    }

    [HttpDelete(ApiRoutes.PhotoLike)]
    public async Task<ActionResult<LikeStateDto>> Unlike(Guid id, CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        var ownerId = await VisiblePhotos(userId).Where(p => p.Id == id).Select(p => (Guid?)p.OwnerId).SingleOrDefaultAsync(ct);
        if (ownerId is null) return NotFound();

        await db.PhotoLikes.Where(l => l.PhotoId == id && l.UserId == userId).ExecuteDeleteAsync(ct);
        await activity.RetractAsync(ownerId.Value, userId, ActivityTypes.Like, photoId: id, ct: ct);
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
        var ownerId = await VisiblePhotos(userId).Where(p => p.Id == id).Select(p => (Guid?)p.OwnerId).SingleOrDefaultAsync(ct);
        if (ownerId is null) return NotFound();
        if (await db.Blocks.AnyAsync(b => b.BlockerId == ownerId && b.BlockedId == userId, ct))
            return Problem("You can't comment on this photo.", statusCode: StatusCodes.Status400BadRequest);

        var text = request.Text.Trim();
        if (text.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Text), "Write something first.");
            return ValidationProblem(ModelState);
        }

        var comment = new PhotoComment { PhotoId = id, AuthorId = userId, Text = text, CreatedAt = clock.GetUtcNow() };
        db.PhotoComments.Add(comment);
        await db.SaveChangesAsync(ct);
        await activity.NotifyAsync(ownerId.Value, userId, ActivityTypes.Comment, photoId: id, snippet: text, ct: ct);

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

    /// <summary>No place, or one of the member's own (places are private, so nobody can link someone else's).</summary>
    private async Task<bool> IsOwnPlaceAsync(Guid? placeId, Guid ownerId, CancellationToken ct) =>
        placeId is null || await db.Places.AnyAsync(p => p.Id == placeId && p.UserId == ownerId, ct);

    /// <summary>Photos this user may see: everything that isn't hidden by moderation, plus their own.</summary>
    private IQueryable<Photo> VisiblePhotos(Guid? userId) =>
        db.Photos.AsNoTracking().Where(Moderation.PhotoVisibleTo(userId));

    private async Task<PhotoDetailDto?> LoadDetailAsync(Guid id, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var me = CurrentUserId();
        // Admins can open a pending photo to review it.
        var isAdmin = await admins.IsAdminAsync(me, ct);
        var commentVisible = Moderation.CommentVisibleTo(me);
        var row = await db.Photos.AsNoTracking().Where(Moderation.PhotoVisibleTo(me, reviewer: isAdmin))
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
                Place = p.Place == null ? null : new PhotoPlaceDto(p.Place.Id, p.Place.Name, p.Place.Latitude, p.Place.Longitude),
                Tags = p.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToList(),
                Featured = db.PhotosOfTheDay.Where(x => x.PhotoId == p.Id && x.Date <= today)
                    .OrderByDescending(x => x.Date).Select(x => (DateOnly?)x.Date).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;

        var p = row.Photo;
        return new PhotoDetailDto(p.Id, p.Title, p.Description,
            storage.PublicUrl(p.DisplayPath), storage.ThumbnailUrl(p.ThumbnailPath), p.Width, p.Height, row.Owner,
            new CameraDetailsDto(p.CameraMake, p.CameraModel, p.Lens, p.FocalLengthMm, p.Aperture, p.ExposureTime, p.Iso, p.TakenAt),
            row.Likes, row.Comments, row.LikedByMe, p.OwnerId == me, p.CreatedAt, p.UpdatedAt,
            Moderation.State(row.Upheld, row.OpenReports),
            CanReport: p.OwnerId != me && !row.ReportedByMe,
            ShowOnHome: p.ShowOnHome,
            Place: row.Place,
            Tags: row.Tags,
            PhotoOfTheDay: row.Featured,
            WantsFeedback: p.WantsFeedback,
            AllowDownload: p.AllowDownload,
            Watermarked: p.Watermarked,
            ReviewStatus: p.ReviewStatus,
            ReviewNote: p.OwnerId == me ? p.ReviewNote : null,
            CanReview: isAdmin && p.OwnerId != me && PhotoReview.CanReview(p.ReviewStatus),
            BeforeImageUrl: p.BeforeDisplayPath is null ? null : storage.PublicUrl(p.BeforeDisplayPath));
    }

    private async Task<LikeStateDto> LikeStateAsync(Guid photoId, Guid userId, CancellationToken ct) => new(
        await db.PhotoLikes.AnyAsync(l => l.PhotoId == photoId && l.UserId == userId, ct),
        await db.PhotoLikes.CountAsync(l => l.PhotoId == photoId, ct));

    // Distinct: the thumbnail path is the display path for photos uploaded with image transforms on.
    private Task TryDeleteFilesAsync(Photo photo) =>
        TryDeleteAsync(new[] { photo.DisplayPath, photo.ThumbnailPath, photo.BeforeDisplayPath }
            .OfType<string>().Distinct().ToList(), photo.Id);

    private async Task TryDeleteAsync(IReadOnlyList<string> paths, Guid photoId)
    {
        try
        {
            await storage.DeleteAsync(paths, CancellationToken.None);
        }
        catch (HttpRequestException ex)
        {
            // The database is the source of truth; an orphaned file is harmless but worth knowing about.
            logger.LogWarning(ex, "Could not delete storage files for photo {PhotoId}", photoId);
        }
    }
}
