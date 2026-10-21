using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class SearchController(IPhotoWorldApiClient api) : Controller
{
    /// <summary>Results for the menu's search box; an empty search goes to Explore.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return RedirectToAction(nameof(Explore));
        var query = q.Trim();
        // "#fog" is a tag: go straight to its photos.
        if (query.StartsWith('#') && !query.Contains(' ') && Shared.Photos.PhotoTags.Normalize(query) is { } tag)
            return RedirectToAction("Index", "Photos", new { tag });
        return View(await api.SearchAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Explore(CancellationToken ct) => View(await api.ExploreAsync(ct));
}
