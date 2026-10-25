using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Places;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

/// <summary>The signed-in member's private map of the places they've been.</summary>
public class PlacesController(IPhotoWorldApiClient api) : Controller
{
    private static readonly Dictionary<string, string> PlaceFields = new()
    {
        ["name"] = nameof(PlaceFormViewModel.Name),
        ["note"] = nameof(PlaceFormViewModel.Note),
        ["latitude"] = nameof(PlaceFormViewModel.Latitude),
        ["longitude"] = nameof(PlaceFormViewModel.Longitude),
        ["visitedOn"] = nameof(PlaceFormViewModel.VisitedOn),
        ["forecastAlerts"] = nameof(PlaceFormViewModel.ForecastAlerts),
    };

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new PlacesPageViewModel(await api.GetPlacesAsync(ct), new PlaceFormViewModel(), FormOpen: false));

    /// <summary>Adds a place, or updates one when the form carries an <see cref="PlaceFormViewModel.Id"/>.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PlaceFormViewModel model, CancellationToken ct)
    {
        if (!model.TryGetCoordinates(out var latitude, out var longitude) && ModelState.IsValid)
        {
            ModelState.AddModelError(nameof(model.Latitude), "Pick the place on the map.");
        }
        if (!ModelState.IsValid) return await IndexWithErrorsAsync(model, ct);

        var request = new SavePlaceRequest(model.Name, model.Note, latitude, longitude, model.VisitedOn, model.ForecastAlerts);
        var result = model.Id is { } id
            ? await api.UpdatePlaceAsync(id, request, ct)
            : await api.CreatePlaceAsync(request, ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, PlaceFields);
            return await IndexWithErrorsAsync(model, ct);
        }

        TempData["Flash"] = model.Id is null ? $"{result.Value!.Name} is on your map." : "Place updated.";
        return RedirectToAction(nameof(Index), new { focus = result.Value!.Id });
    }

    /// <summary>The golden-hour forecast strip for one place, loaded by places.js when the place is opened.</summary>
    [HttpGet]
    public async Task<IActionResult> Forecast(Guid id, CancellationToken ct) =>
        PartialView("_Forecast", await api.GetPlaceForecastAsync(id, ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.DeletePlaceAsync(id, ct) ? "Place removed." : "That place was already removed.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> IndexWithErrorsAsync(PlaceFormViewModel model, CancellationToken ct) =>
        View(nameof(Index), new PlacesPageViewModel(await api.GetPlacesAsync(ct), model, FormOpen: true));
}
