using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class SpotsController(IPhotoWorldApiClient api) : Controller
{
    /// <summary>The map of photo spots; <paramref name="photo"/> opens that photo's pin.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(Guid? photo, CancellationToken ct) =>
        View(new SpotsPageViewModel(await api.GetSpotsAsync(ct: ct), photo));
}
