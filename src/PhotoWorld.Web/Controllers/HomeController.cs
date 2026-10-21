using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

[AllowAnonymous]
public class HomeController(IPhotoWorldApiClient api, IMemoryCache cache, ILogger<HomeController> logger) : Controller
{
    private const string ShowcaseCacheKey = "home-showcase";

    /// <summary>Landing page for everyone, with the slideshow of photos members chose to feature.</summary>
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var showcase = await cache.GetOrCreateAsync(ShowcaseCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            try
            {
                return await api.GetShowcaseAsync(ct);
            }
            catch (HttpRequestException ex)
            {
                // The landing page must still load if the API is down; just skip the slideshow briefly.
                logger.LogWarning(ex, "Could not load the home-page showcase");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                return (IReadOnlyList<ShowcasePhotoDto>)[];
            }
        });
        return View(showcase ?? []);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Terms() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
