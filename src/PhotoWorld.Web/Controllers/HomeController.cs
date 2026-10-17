using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Web.Models;

namespace PhotoWorld.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    /// <summary>Landing page for visitors; members go straight to the gallery.</summary>
    public IActionResult Index()
    {
        return User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Photos") : View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
