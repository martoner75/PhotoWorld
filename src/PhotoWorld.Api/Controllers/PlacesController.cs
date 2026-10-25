using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Places;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// The signed-in member's visited places. Private: every query is scoped to the caller, and someone else's
/// place answers 404 exactly like one that doesn't exist.
/// </summary>
[ApiController]
[Authorize]
public class PlacesController(PhotoWorldDbContext db, OpenMeteoClient weather, TimeProvider clock) : ControllerBase
{
    [HttpGet(ApiRoutes.Places)]
    public async Task<ActionResult<List<PlaceDto>>> List(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        return await db.Places.AsNoTracking()
            .Where(p => p.UserId == me)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PlaceDto(p.Id, p.Name, p.Note, p.Latitude, p.Longitude, p.VisitedOn, p.CreatedAt, p.ForecastAlerts))
            .ToListAsync(ct);
    }

    [HttpPost(ApiRoutes.Places)]
    public async Task<ActionResult<PlaceDto>> Create(SavePlaceRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!await ValidateAsync(request, me, null, ct)) return ValidationProblem(ModelState);
        if (await db.Places.CountAsync(p => p.UserId == me, ct) >= PlaceLimits.MaxPerMember)
        {
            return Problem($"You can pin up to {PlaceLimits.MaxPerMember:N0} places.", statusCode: StatusCodes.Status400BadRequest);
        }

        var now = clock.GetUtcNow();
        var place = new Place { UserId = me, Name = "", CreatedAt = now };
        Apply(place, request, now);
        db.Places.Add(place);
        await db.SaveChangesAsync(ct);
        return ToDto(place);
    }

    [HttpPut(ApiRoutes.Place)]
    public async Task<ActionResult<PlaceDto>> Update(Guid id, SavePlaceRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var place = await db.Places.SingleOrDefaultAsync(p => p.Id == id && p.UserId == me, ct);
        if (place is null) return NotFound();
        if (!await ValidateAsync(request, me, id, ct)) return ValidationProblem(ModelState);

        Apply(place, request, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return ToDto(place);
    }

    /// <summary>
    /// The next few days of golden hours at the place, rated from Open-Meteo's cloud and rain forecast. Only the
    /// rounded coordinates go to the weather service.
    /// </summary>
    [HttpGet(ApiRoutes.PlaceForecast)]
    public async Task<ActionResult<PlaceForecastDto>> Forecast(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var place = await db.Places.AsNoTracking().Where(p => p.Id == id && p.UserId == me)
            .Select(p => new { p.Latitude, p.Longitude }).SingleOrDefaultAsync(ct);
        if (place is null) return NotFound();

        IReadOnlyList<HourlyWeather> hours;
        try
        {
            hours = await weather.HourlyAsync(place.Latitude, place.Longitude, ct);
        }
        catch (ForecastUnavailableException)
        {
            return Problem("The weather forecast isn't available right now. Try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var now = clock.GetUtcNow();
        return new PlaceForecastDto(LightForecast.Windows(SunTimes.LocalDate(now, place.Longitude), LightForecast.DaysAhead,
            place.Latitude, place.Longitude, hours, now));
    }

    [HttpDelete(ApiRoutes.Place)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var deleted = await db.Places.Where(p => p.Id == id && p.UserId == me).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }

    /// <summary>Rules the data annotations can't express.</summary>
    private async Task<bool> ValidateAsync(SavePlaceRequest request, Guid me, Guid? id, CancellationToken ct)
    {
        if (request.ForecastAlerts
            && await db.Places.CountAsync(p => p.UserId == me && p.ForecastAlerts && p.Id != id, ct) >= PlaceLimits.MaxForecastAlerts)
        {
            ModelState.AddModelError(nameof(request.ForecastAlerts),
                $"You can have light forecasts for up to {PlaceLimits.MaxForecastAlerts} places. Turn one off first.");
        }
        if (string.IsNullOrWhiteSpace(request.Name)) ModelState.AddModelError(nameof(request.Name), "Give the place a name.");
        if (!double.IsFinite(request.Latitude) || !double.IsFinite(request.Longitude))
        {
            ModelState.AddModelError(nameof(request.Latitude), "Pick the place on the map.");
        }
        // A day of slack so members ahead of UTC can log today's visit.
        if (request.VisitedOn > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddDays(1)))
        {
            ModelState.AddModelError(nameof(request.VisitedOn), "The visit date can't be in the future.");
        }
        return ModelState.IsValid;
    }

    private static void Apply(Place place, SavePlaceRequest request, DateTimeOffset now)
    {
        place.Name = request.Name.Trim();
        place.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        // ~1 m precision is plenty for a pin, and keeps stored values tidy.
        place.Latitude = Math.Round(request.Latitude, 5);
        place.Longitude = Math.Round(request.Longitude, 5);
        place.VisitedOn = request.VisitedOn;
        place.ForecastAlerts = request.ForecastAlerts;
        place.UpdatedAt = now;
    }

    private static PlaceDto ToDto(Place p) =>
        new(p.Id, p.Name, p.Note, p.Latitude, p.Longitude, p.VisitedOn, p.CreatedAt, p.ForecastAlerts);
}
