using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// The Spots map: photos their owners linked to one of their places. Only those places' names and positions leave
/// the owner's private map, and only through the photos the viewer may see.
/// </summary>
[ApiController]
[Authorize]
public class SpotsController(PhotoWorldDbContext db, SupabaseStorage storage) : ControllerBase
{
    /// <summary>Most spots on the map at once (newest photos first); plenty until PhotoWorld is much bigger.</summary>
    public const int MaxSpots = 2000;

    [HttpGet(ApiRoutes.Spots)]
    public async Task<IReadOnlyList<PhotoSpotDto>> List(Guid? ownerId = null, CancellationToken ct = default)
    {
        var query = db.Photos.AsNoTracking()
            .Where(Moderation.PhotoVisibleTo(User.GetUserId()))
            .Where(p => p.PlaceId != null);
        if (ownerId is not null) query = query.Where(p => p.OwnerId == ownerId);

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(MaxSpots)
            .Select(p => new
            {
                p.Id, p.Title, p.ThumbnailPath, p.OwnerId, Owner = p.Owner.DisplayName,
                Place = p.Place!.Name, p.Place.Latitude, p.Place.Longitude,
            })
            .ToListAsync(ct);

        return rows.Select(r => new PhotoSpotDto(r.Id, r.Title, storage.PublicUrl(r.ThumbnailPath), r.OwnerId, r.Owner,
            r.Place, r.Latitude, r.Longitude)).ToList();
    }
}
