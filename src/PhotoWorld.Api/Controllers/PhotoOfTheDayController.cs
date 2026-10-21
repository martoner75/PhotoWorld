using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>Photo of the day: admins schedule one photo per day (UTC); members see today's, or the latest one.</summary>
[ApiController]
[Authorize]
public class PhotoOfTheDayController(PhotoWorldDbContext db, AdminAccess admins, SupabaseStorage storage, TimeProvider clock,
    ILogger<PhotoOfTheDayController> logger) : ControllerBase
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>Today's photo, or the most recent past one (within a week) while today has none. 204 when there's nothing.</summary>
    [HttpGet(ApiRoutes.PhotoOfTheDay)]
    public async Task<ActionResult<PhotoOfTheDayDto>> Get(CancellationToken ct)
    {
        var me = User.GetUserId();
        var today = Today;
        var weekAgo = today.AddDays(-7);
        var visible = db.Photos.Where(Moderation.PhotoVisibleTo(me));
        var pick = await db.PhotosOfTheDay.AsNoTracking()
            .Where(x => x.Date <= today && x.Date > weekAgo && visible.Any(p => p.Id == x.PhotoId))
            .OrderByDescending(x => x.Date)
            .Select(x => new { x.Date, x.PhotoId, x.Note })
            .FirstOrDefaultAsync(ct);
        if (pick is null) return NoContent();

        var photo = (await db.Photos.AsNoTracking().Where(p => p.Id == pick.PhotoId).ToSummariesAsync(me, storage, ct)).Single();
        return new PhotoOfTheDayDto(pick.Date, photo, pick.Note, pick.Date == today);
    }

    /// <summary>The last two weeks and everything scheduled ahead, newest first.</summary>
    [HttpGet(ApiRoutes.AdminPhotoOfTheDay)]
    public async Task<ActionResult<IReadOnlyList<PhotoOfTheDayScheduleDto>>> Schedule(CancellationToken ct)
    {
        var me = User.GetUserId();
        if (!await admins.IsAdminAsync(me, ct)) return Forbid();
        var from = Today.AddDays(-14);
        var rows = await db.PhotosOfTheDay.AsNoTracking()
            .Where(x => x.Date >= from).OrderByDescending(x => x.Date)
            .Select(x => new { x.Date, x.PhotoId, x.Note, ChosenBy = x.ChosenBy == null ? null : x.ChosenBy.DisplayName })
            .ToListAsync(ct);
        var ids = rows.Select(r => r.PhotoId).Distinct().ToList();
        var photos = (await db.Photos.AsNoTracking().Where(p => ids.Contains(p.Id)).ToSummariesAsync(me, storage, ct)).ToDictionary(p => p.Id);
        return rows.Where(r => photos.ContainsKey(r.PhotoId))
            .Select(r => new PhotoOfTheDayScheduleDto(r.Date, photos[r.PhotoId], r.Note, r.ChosenBy)).ToList();
    }

    /// <summary>Picks (or replaces) the photo for a day from today on. Only photos everyone may see can be picked.</summary>
    [HttpPut(ApiRoutes.AdminPhotoOfTheDayDate)]
    public async Task<IActionResult> Set(string date, SetPhotoOfTheDayRequest request, CancellationToken ct)
    {
        var me = User.GetUserId();
        if (!await admins.IsAdminAsync(me, ct)) return Forbid();
        if (!TryParse(date, out var day)) return Problem("Use a date like 2026-11-01.", statusCode: StatusCodes.Status400BadRequest);
        if (day < Today) return Problem("Pick today or a later day.", statusCode: StatusCodes.Status400BadRequest);
        if (!await db.Photos.Where(Moderation.PhotoVisibleTo(null)).AnyAsync(p => p.Id == request.PhotoId, ct))
            return Problem("That photo can't be featured (it's hidden or no longer exists).", statusCode: StatusCodes.Status400BadRequest);

        var pick = await db.PhotosOfTheDay.FindAsync([day], ct);
        if (pick is null) db.PhotosOfTheDay.Add(pick = new PhotoOfTheDay { Date = day, PhotoId = request.PhotoId, CreatedAt = clock.GetUtcNow() });
        else if (pick.PhotoId != request.PhotoId) pick.NotifiedAt = null; // a new photo: tell its owner
        pick.PhotoId = request.PhotoId;
        pick.ChosenById = me;
        pick.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Admin {AdminId} set photo {PhotoId} as photo of the day for {Date}", me, request.PhotoId, day);
        return NoContent();
    }

    [HttpDelete(ApiRoutes.AdminPhotoOfTheDayDate)]
    public async Task<IActionResult> Clear(string date, CancellationToken ct)
    {
        if (!await admins.IsAdminAsync(User.GetUserId(), ct)) return Forbid();
        if (!TryParse(date, out var day)) return NotFound();
        return await db.PhotosOfTheDay.Where(x => x.Date == day).ExecuteDeleteAsync(ct) == 0 ? NotFound() : NoContent();
    }

    private static bool TryParse(string text, out DateOnly day) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
}
