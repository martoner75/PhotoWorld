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

    /// <summary>A site-wide challenge (site admins), or with <paramref name="clubId"/> one for that club (its owner and admins).</summary>
    [HttpGet]
    public async Task<IActionResult> New(Guid? clubId, CancellationToken ct)
    {
        var model = ChallengeFormViewModel.ForNext(DateTimeOffset.UtcNow);
        if (clubId is { } id)
        {
            var club = await api.GetClubAsync(id, ct);
            if (club is null || !club.CanManageMembers) return Forbid();
            (model.ClubId, model.ClubName) = (id, club.Summary.Name);
        }
        else if (!await IsAdminAsync(ct))
        {
            return Forbid();
        }
        return View("Form", model);
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
        TempData["Flash"] = model.ClubId is null
            ? $"Challenge “{result.Value!.Title}” created."
            : $"Club challenge “{result.Value!.Title}” created. Members hear when it opens.";
        return model.ClubId is { } club ? RedirectToAction(nameof(Details), new { id = result.Value!.Id }) : RedirectToAction(nameof(Manage));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var challenge = await api.GetChallengeAsync(id, ct);
        if (challenge is null) return NotFound();
        return challenge.CanManage ? View("Form", ChallengeFormViewModel.From(challenge.Challenge)) : Forbid();
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
        return model.ClubId is null ? RedirectToAction(nameof(Manage)) : RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Deletes a challenge; with <paramref name="clubId"/> (from a club challenge's page), back to the club.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, Guid? clubId, CancellationToken ct)
    {
        TempData["Flash"] = await api.DeleteChallengeAsync(id, ct)
            ? "Challenge deleted. Its photos are still in members' galleries."
            : "That challenge no longer exists, or you can't delete it.";
        return clubId is { } club ? RedirectToAction("Details", "Clubs", new { id = club }) : RedirectToAction(nameof(Manage));
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
