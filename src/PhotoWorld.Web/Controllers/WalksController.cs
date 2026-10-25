using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Walks;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class WalksController(IPhotoWorldApiClient api) : Controller
{
    private static readonly Dictionary<string, string> FormFields = new()
    {
        ["title"] = nameof(WalkFormViewModel.Title),
        ["meetingPoint"] = nameof(WalkFormViewModel.MeetingPoint),
        ["startsAt"] = nameof(WalkFormViewModel.StartsLocal),
        ["endsAt"] = nameof(WalkFormViewModel.DurationMinutes),
        ["maxPeople"] = nameof(WalkFormViewModel.MaxPeople),
    };

    [HttpGet]
    public async Task<IActionResult> Index(string? list, CancellationToken ct)
    {
        list = list is WalkLists.Past or WalkLists.Mine ? list : WalkLists.Upcoming;
        return View(new WalksPageViewModel(list, await api.GetWalksAsync(list, ct)));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var walk = await api.GetWalkAsync(id, ct);
        return walk is null ? NotFound() : View(walk);
    }

    /// <summary>A new walk; with <paramref name="clubId"/> (from a club's page) it starts as that club's walk.</summary>
    [HttpGet]
    public async Task<IActionResult> New(Guid? clubId, CancellationToken ct) =>
        await FormAsync(new WalkFormViewModel { ClubId = clubId }, ct);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> New(WalkFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await FormAsync(model, ct);
        var result = await api.CreateWalkAsync(model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return await FormAsync(model, ct);
        }
        TempData["Flash"] = model.ClubId is null
            ? "Your walk is on. Members who follow you will hear about it."
            : "Your club walk is on. The club's members will hear about it; only they can see it.";
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var walk = await api.GetWalkAsync(id, ct);
        if (walk is null || !walk.CanEdit) return NotFound();
        var model = WalkFormViewModel.From(walk);
        return await FormAsync(model, ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, WalkFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return await FormAsync(model, ct);
        var result = await api.UpdateWalkAsync(id, model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return await FormAsync(model, ct);
        }
        TempData["Flash"] = "Walk saved. If the time or meeting point changed, everyone going has been told.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.CancelWalkAsync(id, ct) ? "Walk cancelled. Everyone going has been told." : "That walk can't be cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.DeleteWalkAsync(id, ct) ? "Walk deleted." : "That walk can't be deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Attend(Guid id, bool going, CancellationToken ct)
    {
        TempData["Flash"] = await api.SetWalkAttendanceAsync(id, going, ct)
            ?? (going ? "You're going! You'll get a reminder the day before." : "You're no longer going.");
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IActionResult> FormAsync(WalkFormViewModel model, CancellationToken ct)
    {
        model.Places = await api.GetPlacesAsync(ct);
        if (model.Id is null)
            model.Clubs = (await api.GetMyClubsAsync(ct))
                .Where(c => c.ReviewStatus == Shared.Clubs.ClubReviewStatus.Approved && c.MyStatus == Shared.Clubs.ClubMemberStatus.Active)
                .ToList();
        return View("Form", model);
    }
}
