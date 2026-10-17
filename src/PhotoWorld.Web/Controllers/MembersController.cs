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
        return View(new MemberPageViewModel(member, await api.GetPhotosAsync(page, id, ct)));
    }

    /// <summary>Shortcut to the signed-in member's own page.</summary>
    [HttpGet]
    public IActionResult Me() => RedirectToAction(nameof(Details), new { id = User.GetUserId() });
}
