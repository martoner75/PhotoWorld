using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Collections;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// Members' photo collections. A private collection answers 404 to everyone but its owner, like one that doesn't
/// exist; photos inside are filtered by the usual visibility rule for the viewer.
/// </summary>
[ApiController]
[Authorize]
public class CollectionsController(PhotoWorldDbContext db, SupabaseStorage storage, TimeProvider clock, ILogger<CollectionsController> logger)
    : ControllerBase
{
    private const int PageSize = 24;

    /// <summary>The caller's collections (all), or another member's public ones when <paramref name="ownerId"/> is set.</summary>
    [HttpGet(ApiRoutes.Collections)]
    public async Task<ActionResult<IReadOnlyList<CollectionSummaryDto>>> List(Guid? ownerId = null, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var query = Visible(me).Where(c => c.OwnerId == (ownerId ?? me));
        return await SummariesAsync(query.OrderByDescending(c => c.UpdatedAt), me, ct);
    }

    [HttpGet(ApiRoutes.Collection)]
    public async Task<ActionResult<CollectionDto>> Get(Guid id, int page = 1, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var summary = (await SummariesAsync(Visible(me).Where(c => c.Id == id), me, ct)).SingleOrDefault();
        if (summary is null) return NotFound();

        page = Math.Max(1, page);
        var photos = db.CollectionPhotos.Where(x => x.CollectionId == id)
            .Where(x => db.Photos.Where(Moderation.PhotoVisibleTo(me)).Any(p => p.Id == x.PhotoId));
        var total = await photos.CountAsync(ct);
        var items = await photos.OrderByDescending(x => x.AddedAt)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(x => x.Photo)
            .ToSummariesAsync(me, storage, ct);
        return new CollectionDto(summary, new PagedResult<PhotoSummaryDto>(items, page, PageSize, total));
    }

    [HttpPost(ApiRoutes.Collections)]
    public async Task<ActionResult<CollectionSummaryDto>> Create(SaveCollectionRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (await db.Collections.CountAsync(c => c.OwnerId == me, ct) >= CollectionLimits.MaxPerMember)
            return Problem($"You can have up to {CollectionLimits.MaxPerMember} collections.", statusCode: StatusCodes.Status400BadRequest);

        var now = clock.GetUtcNow();
        var collection = new Collection
        {
            OwnerId = me, Title = request.Title.Trim(), Description = Clean(request.Description), IsPublic = request.IsPublic,
            CreatedAt = now, UpdatedAt = now,
        };
        db.Collections.Add(collection);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} created collection {CollectionId}", me, collection.Id);
        var summary = (await SummariesAsync(db.Collections.Where(c => c.Id == collection.Id), me, ct)).Single();
        return CreatedAtAction(nameof(Get), new { id = collection.Id }, summary);
    }

    [HttpPut(ApiRoutes.Collection)]
    public async Task<ActionResult<CollectionSummaryDto>> Update(Guid id, SaveCollectionRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var collection = await db.Collections.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == me, ct);
        if (collection is null) return NotFound();

        collection.Title = request.Title.Trim();
        collection.Description = Clean(request.Description);
        collection.IsPublic = request.IsPublic;
        collection.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return (await SummariesAsync(db.Collections.Where(c => c.Id == id), me, ct)).Single();
    }

    /// <summary>Deletes the collection; the photos in it aren't touched.</summary>
    [HttpDelete(ApiRoutes.Collection)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var deleted = await db.Collections.Where(c => c.Id == id && c.OwnerId == me).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }

    /// <summary>Adds a photo the caller can see to one of their collections (adding twice is fine).</summary>
    [HttpPut(ApiRoutes.CollectionPhoto)]
    public async Task<IActionResult> AddPhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var collection = await db.Collections.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == me, ct);
        if (collection is null || !await db.Photos.Where(Moderation.PhotoVisibleTo(me)).AnyAsync(p => p.Id == photoId, ct))
            return NotFound();
        if (await db.CollectionPhotos.AnyAsync(x => x.CollectionId == id && x.PhotoId == photoId, ct)) return NoContent();
        if (await db.CollectionPhotos.CountAsync(x => x.CollectionId == id, ct) >= CollectionLimits.MaxPhotos)
            return Problem($"A collection can hold up to {CollectionLimits.MaxPhotos:N0} photos.", statusCode: StatusCodes.Status400BadRequest);

        var now = clock.GetUtcNow();
        db.CollectionPhotos.Add(new CollectionPhoto { CollectionId = id, PhotoId = photoId, AddedAt = now });
        collection.UpdatedAt = now;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* Added twice at the same moment: it's in. */ }
        return NoContent();
    }

    [HttpDelete(ApiRoutes.CollectionPhoto)]
    public async Task<IActionResult> RemovePhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!await db.Collections.AnyAsync(c => c.Id == id && c.OwnerId == me, ct)) return NotFound();
        await db.CollectionPhotos.Where(x => x.CollectionId == id && x.PhotoId == photoId).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    /// <summary>For the "Save" choice on a photo: the caller's collections, marking those that already hold it.</summary>
    [HttpGet(ApiRoutes.PhotoCollections)]
    public async Task<ActionResult<IReadOnlyList<CollectionChoiceDto>>> ForPhoto(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        return await db.Collections.AsNoTracking()
            .Where(c => c.OwnerId == me)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new CollectionChoiceDto(c.Id, c.Title, c.IsPublic, c.Photos.Any(x => x.PhotoId == id)))
            .ToListAsync(ct);
    }

    /// <summary>Collections the viewer may open: their own, and other members' public ones (not blocked or suspended).</summary>
    private IQueryable<Collection> Visible(Guid me) => db.Collections.AsNoTracking().Where(Moderation.CollectionVisibleTo(me));

    private async Task<List<CollectionSummaryDto>> SummariesAsync(IQueryable<Collection> query, Guid me, CancellationToken ct)
    {
        var photos = db.Photos.Where(Moderation.PhotoVisibleTo(me));
        var rows = await query
            .Select(c => new
            {
                c.Id, c.Title, c.Description, c.IsPublic, c.UpdatedAt, c.OwnerId,
                Owner = new UserSummaryDto(c.Owner.Id, c.Owner.DisplayName, c.Owner.AvatarUrl),
                Count = c.Photos.Count(x => photos.Any(p => p.Id == x.PhotoId)),
                Cover = c.Photos.Where(x => photos.Any(p => p.Id == x.PhotoId))
                    .OrderByDescending(x => x.AddedAt).Select(x => x.Photo.ThumbnailPath).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return rows.Select(r => new CollectionSummaryDto(r.Id, r.Title, r.Description, r.IsPublic, r.Owner, r.Count,
            r.Cover is null ? null : storage.PublicUrl(r.Cover), r.UpdatedAt, r.OwnerId == me)).ToList();
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
