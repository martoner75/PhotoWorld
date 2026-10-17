using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>The public home-page slideshow: the only photo data available without signing in.</summary>
[ApiController]
[AllowAnonymous]
public class ShowcaseController(PhotoWorldDbContext db, SupabaseStorage storage) : ControllerBase
{
    public const int MaxPhotos = 12;

    [HttpGet(ApiRoutes.Showcase)]
    public async Task<IReadOnlyList<ShowcasePhotoDto>> Get(CancellationToken ct)
    {
        // Opted-in photos that a signed-out visitor may see: anything hidden by reports is left out.
        var rows = await db.Photos.AsNoTracking()
            .Where(p => p.ShowOnHome)
            .Where(Moderation.PhotoVisibleTo(null))
            .OrderByDescending(p => p.CreatedAt)
            .Take(MaxPhotos)
            .Select(p => new { p.Id, p.Title, p.DisplayPath, p.Width, p.Height, Owner = p.Owner.DisplayName })
            .ToListAsync(ct);

        return rows.Select(r => new ShowcasePhotoDto(r.Id, r.Title, storage.PublicUrl(r.DisplayPath), r.Width, r.Height, r.Owner)).ToList();
    }
}
