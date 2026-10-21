using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Web.Authentication;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class ChallengesController(IPhotoWorldApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await api.GetChallengesAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var challenge = await api.GetChallengeAsync(id, ct);
        return challenge is null ? NotFound() : View(challenge);
    }

    /// <summary>Pick one of your own photos to enter.</summary>
    [HttpGet]
    public async Task<IActionResult> Enter(Guid id, int page = 1, CancellationToken ct = default)
    {
        var challenge = await api.GetChallengeAsync(id, ct);
        if (challenge is null) return NotFound();
        if (challenge.Challenge.Phase != ChallengePhase.Open) return RedirectToAction(nameof(Details), new { id });

        var photos = await api.GetPhotosAsync(page, User.GetUserId(), ct: ct);
        return View(new EnterChallengeViewModel(challenge, photos));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enter(Guid id, Guid photoId, CancellationToken ct)
    {
        var problem = await api.EnterChallengeAsync(id, photoId, ct);
        TempData["Flash"] = problem ?? "Your photo is in! Votes are the likes it gets until the challenge ends.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(Guid id, Guid photoId, CancellationToken ct)
    {
        var problem = await api.WithdrawChallengeEntryAsync(id, photoId, ct);
        TempData["Flash"] = problem ?? "Your photo was withdrawn from the challenge.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
