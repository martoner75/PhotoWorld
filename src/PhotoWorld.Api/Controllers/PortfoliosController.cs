using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Portfolios;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// Public portfolios: the one part of PhotoWorld a photographer can share with anyone. Shows only photos a
/// signed-out visitor may see (approved, not hidden by reports, owner not suspended), and only those the owner chose.
/// </summary>
[ApiController]
[Authorize]
public class PortfoliosController(
    PhotoWorldDbContext db,
    SupabaseStorage storage,
    TimeProvider clock,
    ILogger<PortfoliosController> logger) : ControllerBase
{
    [HttpGet(ApiRoutes.PublicPortfolio)]
    [AllowAnonymous]
    public async Task<ActionResult<PublicPortfolioDto>> Get(string slug, CancellationToken ct)
    {
        slug = slug.Trim().ToLowerInvariant();
        var portfolio = await db.Portfolios.AsNoTracking()
            .Where(p => p.Slug == slug && p.IsPublic && p.User.SuspendedAt == null)
            .Select(p => new { p.UserId, p.Slug, p.User.DisplayName, p.User.AvatarUrl, p.Bio, p.Website, p.Instagram, p.ContactEmail })
            .SingleOrDefaultAsync(ct);
        if (portfolio is null) return NotFound();

        var publicPhotos = db.Photos.Where(Moderation.PhotoVisibleTo(null));
        var photos = await db.PortfolioPhotos.AsNoTracking()
            .Where(x => x.UserId == portfolio.UserId && publicPhotos.Any(p => p.Id == x.PhotoId))
            .OrderBy(x => x.Position)
            .Select(x => new { x.Photo.Id, x.Photo.Title, x.Photo.Description, x.Photo.DisplayPath, x.Photo.ThumbnailPath,
                x.Photo.Width, x.Photo.Height, x.Photo.AllowDownload })
            .ToListAsync(ct);

        return new PublicPortfolioDto(portfolio.Slug, portfolio.DisplayName, portfolio.AvatarUrl, portfolio.Bio,
            portfolio.Website, portfolio.Instagram, portfolio.ContactEmail,
            photos.Select(p => new PortfolioPhotoDto(p.Id, p.Title, p.Description, storage.PublicUrl(p.DisplayPath),
                storage.PublicUrl(p.ThumbnailPath), p.Width, p.Height, p.AllowDownload)).ToList());
    }

    [HttpGet(ApiRoutes.MyPortfolio)]
    public async Task<ActionResult<MyPortfolioDto>> Mine(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var portfolio = await db.Portfolios.AsNoTracking().Include(p => p.Photos).SingleOrDefaultAsync(p => p.UserId == me, ct);
        var chosen = portfolio?.Photos.OrderBy(x => x.Position).Select(x => x.PhotoId).ToList() ?? [];

        // The owner's photos that could be shown publicly: the newest, plus any already chosen.
        var publicOwn = db.Photos.AsNoTracking().Where(p => p.OwnerId == me).Where(Moderation.PhotoVisibleTo(null));
        var newest = await publicOwn.OrderByDescending(p => p.CreatedAt).Take(PortfolioLimits.CandidatesMax)
            .Select(p => p.Id).ToListAsync(ct);
        var candidates = await publicOwn
            .Where(p => newest.Contains(p.Id) || chosen.Contains(p.Id))
            .OrderByDescending(p => p.CreatedAt)
            .ToSummariesAsync(me, storage, ct);

        var slug = portfolio?.Slug
                   ?? await AvailableSlugAsync(PortfolioRules.SuggestSlug(
                       await db.Users.Where(u => u.Id == me).Select(u => u.DisplayName).SingleAsync(ct)), me, ct);
        return new MyPortfolioDto(portfolio?.IsPublic ?? false, slug, portfolio?.Bio, portfolio?.Website,
            portfolio?.Instagram, portfolio?.ContactEmail, chosen.Where(id => candidates.Any(c => c.Id == id)).ToList(), candidates);
    }

    [HttpPut(ApiRoutes.MyPortfolio)]
    public async Task<ActionResult<MyPortfolioDto>> Save(SavePortfolioRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me || !await db.Users.AnyAsync(u => u.Id == me, ct)) return Unauthorized();

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (!PortfolioRules.IsValidSlug(slug))
            ModelState.AddModelError(nameof(request.Slug),
                $"Use {PortfolioLimits.SlugMin}–{PortfolioLimits.SlugMax} lower-case letters, numbers and single hyphens.");
        else if (await db.Portfolios.AnyAsync(p => p.Slug == slug && p.UserId != me, ct))
            ModelState.AddModelError(nameof(request.Slug), "That address is taken. Try another.");

        var website = PortfolioRules.Website(request.Website);
        if (!string.IsNullOrWhiteSpace(request.Website) && website is null)
            ModelState.AddModelError(nameof(request.Website), "Enter a web address, like https://example.com.");
        var instagram = PortfolioRules.InstagramHandle(request.Instagram);
        if (!string.IsNullOrWhiteSpace(request.Instagram) && instagram is null)
            ModelState.AddModelError(nameof(request.Instagram), "Enter your Instagram name, like @ana.silva.");

        var ids = request.PhotoIds.Distinct().ToList();
        if (ids.Count > PortfolioLimits.PhotosMax)
            ModelState.AddModelError(nameof(request.PhotoIds), $"A portfolio can show up to {PortfolioLimits.PhotosMax} photos.");
        var allowed = await db.Photos.Where(p => p.OwnerId == me && ids.Contains(p.Id)).Where(Moderation.PhotoVisibleTo(null))
            .Select(p => p.Id).ToListAsync(ct);
        if (allowed.Count != ids.Count)
            ModelState.AddModelError(nameof(request.PhotoIds), "Choose your own photos that are approved and visible to everyone.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var portfolio = await db.Portfolios.Include(p => p.Photos).SingleOrDefaultAsync(p => p.UserId == me, ct);
        if (portfolio is null)
        {
            portfolio = new Portfolio { UserId = me, Slug = slug };
            db.Portfolios.Add(portfolio);
        }
        portfolio.Slug = slug;
        portfolio.IsPublic = request.IsPublic;
        portfolio.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        portfolio.Website = website;
        portfolio.Instagram = instagram;
        portfolio.ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim();
        portfolio.UpdatedAt = clock.GetUtcNow();
        portfolio.Photos.Clear();
        portfolio.Photos.AddRange(ids.Select((id, i) => new PortfolioPhoto { UserId = me, PhotoId = id, Position = i }));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Someone took the address at the same moment (the unique index refused it)?
            if (!await db.Portfolios.AsNoTracking().AnyAsync(p => p.Slug == slug && p.UserId != me, ct)) throw;
            ModelState.AddModelError(nameof(request.Slug), "That address is taken. Try another.");
            return ValidationProblem(ModelState);
        }

        logger.LogInformation("User {UserId} saved their portfolio ({Visibility}, {Count} photos)",
            me, portfolio.IsPublic ? "public" : "off", ids.Count);
        return await Mine(ct);
    }

    /// <summary>The suggestion, or the suggestion with a number on the end if someone has it.</summary>
    private async Task<string> AvailableSlugAsync(string suggestion, Guid me, CancellationToken ct)
    {
        for (var n = 1; n < 100; n++)
        {
            var candidate = n == 1 ? suggestion : $"{suggestion[..Math.Min(suggestion.Length, PortfolioLimits.SlugMax - 3)]}-{n}";
            if (!await db.Portfolios.AnyAsync(p => p.Slug == candidate && p.UserId != me, ct)) return candidate;
        }
        return $"photographer-{me.ToString("N")[..8]}";
    }
}
