using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Web.Authentication;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class MembersController(IPhotoWorldApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, int page = 1, CancellationToken ct = default)
    {
        var member = await api.GetMemberAsync(id, ct);
        if (member is null) return NotFound();
        return View(new MemberPageViewModel(member, await api.GetPhotosAsync(page, id, ct: ct)));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Follow(Guid id, bool follow, CancellationToken ct)
    {
        var state = await api.SetFollowAsync(id, follow, ct);
        if (state is null) return NotFound();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(Guid id, bool block, string? returnUrl, CancellationToken ct)
    {
        if (!await api.SetBlockAsync(id, block, ct)) return NotFound();
        TempData["Flash"] = block
            ? "Blocked. You won't see their photos, posts or comments, and they can't follow you or comment on your work."
            : "Unblocked.";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Shortcut to the signed-in member's own page.</summary>
    [HttpGet]
    public IActionResult Me() => RedirectToAction(nameof(Details), new { id = User.GetUserId() });
}
