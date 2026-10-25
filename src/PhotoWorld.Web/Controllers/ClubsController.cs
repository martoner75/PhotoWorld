using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

/// <summary>Camera clubs: the directory, a club's page, running one and joining one.</summary>
public class ClubsController(IPhotoWorldApiClient api) : Controller
{
    private static readonly Dictionary<string, string> FormFields = new()
    {
        ["name"] = nameof(ClubFormViewModel.Name),
        ["city"] = nameof(ClubFormViewModel.City),
        ["description"] = nameof(ClubFormViewModel.Description),
        ["joinPolicy"] = nameof(ClubFormViewModel.JoinPolicy),
        ["latitude"] = nameof(ClubFormViewModel.Area),
    };

    [HttpGet]
    public async Task<IActionResult> Index(string? q, Guid? near, int page = 1, CancellationToken ct = default) =>
        View(new ClubsPageViewModel(false, q, near, await api.GetPlacesAsync(ct), await api.GetClubsAsync(q, near, Math.Max(1, page), ct)));

    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var mine = await api.GetMyClubsAsync(ct);
        return View("Index", new ClubsPageViewModel(true, null, null, [],
            new PagedResult<ClubSummaryDto>(mine.ToList(), 1, Math.Max(1, mine.Count), mine.Count)));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var club = await api.GetClubAsync(id, ct);
        if (club is null) return NotFound();
        var members = await api.GetClubMembersAsync(id, pending: false, ct);
        var requests = club.CanManageMembers ? await api.GetClubMembersAsync(id, pending: true, ct) : [];
        return View(new ClubPageViewModel(club, members, requests));
    }

    [HttpGet]
    public async Task<IActionResult> New(CancellationToken ct) => await FormAsync(new ClubFormViewModel(), ct);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> New(ClubFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await FormAsync(model, ct);
        var result = await api.CreateClubAsync(model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return await FormAsync(model, ct);
        }
        TempData["Flash"] = result.Value!.Summary.ReviewStatus == ClubReviewStatus.Approved
            ? "Your club is open. Share its page so people can join."
            : "Your club is waiting for an admin to approve it. You'll hear under Activity, usually within a day.";
        return RedirectToAction(nameof(Details), new { id = result.Value.Summary.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var club = await api.GetClubAsync(id, ct);
        return club is null || !club.CanEdit ? NotFound() : await FormAsync(ClubFormViewModel.From(club), ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ClubFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return await FormAsync(model, ct);
        var result = await api.UpdateClubAsync(id, model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return await FormAsync(model, ct);
        }
        TempData["Flash"] = "Club saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.DeleteClubAsync(id, ct) ? "Club deleted." : "Only the owner can delete a club.";
        return RedirectToAction(nameof(Mine));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(Guid id, CancellationToken ct)
    {
        if (await api.JoinClubAsync(id, ct) is { } error)
        {
            TempData["Flash"] = error;
        }
        else
        {
            var club = await api.GetClubAsync(id, ct);
            TempData["Flash"] = club?.Summary.MyStatus == ClubMemberStatus.Pending
                ? "Request sent. You'll hear under Activity when the club lets you in."
                : "Welcome to the club!";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.LeaveClubAsync(id, ct) ?? "You've left the club.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, Guid userId, CancellationToken ct)
    {
        TempData["Flash"] = await api.ApproveClubMemberAsync(id, userId, ct) ?? "They're in. We've let them know.";
        return Redirect(Url.Action(nameof(Details), new { id }) + "#requests");
    }

    /// <summary>Removes a member, or turns down a request to join.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id, Guid userId, bool request, CancellationToken ct)
    {
        TempData["Flash"] = await api.RemoveClubMemberAsync(id, userId, ct) ?? (request ? "Request turned down." : "Member removed.");
        return Redirect(Url.Action(nameof(Details), new { id }) + (request ? "#requests" : "#members"));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Role(Guid id, Guid userId, string role, CancellationToken ct)
    {
        TempData["Flash"] = await api.SetClubRoleAsync(id, userId, role, ct) ?? role switch
        {
            ClubRoles.Owner => "You've handed the club over. You're still an admin.",
            ClubRoles.Admin => "They're now an admin.",
            _ => "They're now a member.",
        };
        return Redirect(Url.Action(nameof(Details), new { id }) + "#members");
    }

    [HttpGet]
    public async Task<IActionResult> Report(Guid id, CancellationToken ct)
    {
        var model = await ReportModelAsync(new ReportViewModel { Id = id }, ct);
        return model is null ? NotFound() : View("Report", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(Guid id, ReportViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            var reloaded = await ReportModelAsync(model, ct);
            return reloaded is null ? NotFound() : View("Report", reloaded);
        }

        var outcome = await api.ReportClubAsync(id, new CreateReportRequest(model.Reason!, model.Note), ct);
        TempData["Flash"] = outcome switch
        {
            ReportOutcome.Reported => "Thanks for letting us know. We'll take a look.",
            ReportOutcome.AlreadyReported => "You've already reported this club.",
            ReportOutcome.NotAllowed => "You can't report your own club.",
            _ => "That club is no longer available.",
        };
        return outcome == ReportOutcome.NotFound ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { id });
    }

    private async Task<ReportViewModel?> ReportModelAsync(ReportViewModel model, CancellationToken ct)
    {
        var club = await api.GetClubAsync(model.Id, ct);
        if (club is null) return null;
        model.Subject = "club";
        model.Title = club.Summary.Name;
        model.QuotedText = club.Description;
        model.CancelUrl = Url.Action(nameof(Details), new { id = model.Id });
        return model;
    }

    private async Task<IActionResult> FormAsync(ClubFormViewModel model, CancellationToken ct)
    {
        model.Places = await api.GetPlacesAsync(ct);
        return View("Form", model);
    }
}
