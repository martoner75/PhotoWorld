using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class ActivityController(IPhotoWorldApiClient api) : Controller
{
    /// <summary>The member's notifications. Opening the first page marks them all read (new ones stay highlighted this time).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var activity = await api.GetActivityAsync(page, ct);
        if (page == 1 && activity.Items.Any(a => !a.IsRead)) await api.MarkActivityReadAsync(ct);
        return View(activity);
    }

    /// <summary>For the bell in the menu (site.js asks once per page).</summary>
    [HttpGet]
    public async Task<IActionResult> Unread(CancellationToken ct) => Json(new UnreadActivityDto(await api.GetUnreadActivityAsync(ct)));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(ActivitySettingsDto settings, CancellationToken ct)
    {
        TempData["Flash"] = await api.SaveActivitySettingsAsync(settings, ct) is null
            ? "We couldn't save your activity settings."
            : "Activity settings saved.";
        return Redirect(Url.Action("Settings", "Account") + "#activity");
    }
}
