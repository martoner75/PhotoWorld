using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Web.Authentication;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class ChallengesController(IPhotoWorldApiClient api) : Controller
{
    private static readonly Dictionary<string, string> FormFields = new()
    {
        ["title"] = nameof(ChallengeFormViewModel.Title),
        ["description"] = nameof(ChallengeFormViewModel.Description),
        ["startsAt"] = nameof(ChallengeFormViewModel.StartsAt),
        ["endsAt"] = nameof(ChallengeFormViewModel.EndsAt),
    };

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new ChallengesIndexViewModel(await api.GetChallengesAsync(ct), await IsAdminAsync(ct)));

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

    // ----- Admin pages (members with users.is_admin; the API checks again) -----

    [HttpGet]
    public async Task<IActionResult> Manage(CancellationToken ct)
    {
        if (!await IsAdminAsync(ct)) return Forbid();
        return View(await api.GetChallengesAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> New(CancellationToken ct)
    {
        if (!await IsAdminAsync(ct)) return Forbid();
        return View("Form", ChallengeFormViewModel.ForNext(DateTimeOffset.UtcNow));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> New(ChallengeFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", model);
        var result = await api.CreateChallengeAsync(model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return View("Form", model);
        }
        TempData["Flash"] = $"Challenge “{result.Value!.Title}” created.";
        return RedirectToAction(nameof(Manage));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        if (!await IsAdminAsync(ct)) return Forbid();
        var challenge = await api.GetChallengeAsync(id, ct);
        return challenge is null ? NotFound() : View("Form", ChallengeFormViewModel.From(challenge.Challenge));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ChallengeFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", model);
        var result = await api.UpdateChallengeAsync(id, model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return View("Form", model);
        }
        TempData["Flash"] = "Challenge saved.";
        return RedirectToAction(nameof(Manage));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.DeleteChallengeAsync(id, ct)
            ? "Challenge deleted. Its photos are still in members' galleries."
            : "That challenge no longer exists, or you can't delete it.";
        return RedirectToAction(nameof(Manage));
    }

    private async Task<bool> IsAdminAsync(CancellationToken ct) => (await api.GetCurrentUserAsync(ct))?.IsAdmin == true;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(Guid id, Guid photoId, CancellationToken ct)
    {
        var problem = await api.WithdrawChallengeEntryAsync(id, photoId, ct);
        TempData["Flash"] = problem ?? "Your photo was withdrawn from the challenge.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
