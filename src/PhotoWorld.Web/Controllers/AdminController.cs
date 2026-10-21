using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Admin;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

/// <summary>Admin pages. The API decides who's an admin and checks every call; these pages just show its answers.</summary>
public class AdminController(IPhotoWorldApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Index() => RedirectToAction(nameof(Users));

    [HttpGet]
    public async Task<IActionResult> Users(int page = 1, string? search = null, CancellationToken ct = default)
    {
        var users = await api.GetAdminUsersAsync(page, search, ct);
        if (users is null) return Forbid();
        return View(new AdminUsersViewModel(users, search?.Trim()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAdmin(Guid id, bool isAdmin, string? returnUrl, CancellationToken ct)
    {
        TempData["Flash"] = await api.SetAdminAsync(id, isAdmin, ct)
            ?? (isAdmin ? "They're now an admin. It shows in their menu next time they sign in." : "Admin rights removed.");
        return BackTo(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id, string? reason, string? returnUrl, CancellationToken ct)
    {
        if (reason?.Length > AdminLimits.SuspensionReasonMax) reason = reason[..AdminLimits.SuspensionReasonMax];
        TempData["Flash"] = await api.SetSuspensionAsync(id, new SetSuspensionRequest(true, reason), ct)
            ?? "Account suspended. They're signed out within a minute, and everything they shared is hidden.";
        return BackTo(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, string? returnUrl, CancellationToken ct)
    {
        TempData["Flash"] = await api.SetSuspensionAsync(id, new SetSuspensionRequest(false, null), ct)
            ?? "Account reactivated. They can sign in again and their photos and posts are back.";
        return BackTo(returnUrl);
    }

    private IActionResult BackTo(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Users));
}
