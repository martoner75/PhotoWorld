using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Collabs;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

/// <summary>Collabs: the board of calls, a call's page with replies and its chat, and the call form.</summary>
public class CollabsController(IPhotoWorldApiClient api) : Controller
{
    private static readonly Dictionary<string, string> FormFields = new()
    {
        ["title"] = nameof(CollabFormViewModel.Title),
        ["role"] = nameof(CollabFormViewModel.Role),
        ["terms"] = nameof(CollabFormViewModel.Terms),
        ["area"] = nameof(CollabFormViewModel.Area),
        ["startsAt"] = nameof(CollabFormViewModel.StartsLocal),
        ["endsAt"] = nameof(CollabFormViewModel.DurationMinutes),
        ["placeId"] = nameof(CollabFormViewModel.PlaceId),
        ["photoIds"] = nameof(CollabFormViewModel.PhotoIds),
    };

    [HttpGet]
    public async Task<IActionResult> Index(string? list, string? role, string? terms, string? q, string? nearPlace, double? km, int page = 1,
        CancellationToken ct = default)
    {
        // Empty or "anywhere": the member chose Anywhere. Missing: near their first place.
        var near = Guid.TryParse(nearPlace, out var placeId) ? placeId : (Guid?)null;
        list = list == CollabLists.Mine ? list : CollabLists.Open;
        role = role is not null && CollabRoles.All.Contains(role) ? role : null;
        terms = terms is not null && CollabTerms.All.Contains(terms) ? terms : null;
        var places = await api.GetPlacesAsync(ct);
        // Near the member's first place unless they chose another (or none, with an empty value).
        if (nearPlace is null && places.Count > 0) near = places[0].Id;
        if (near is { } chosen && places.All(p => p.Id != chosen)) near = null;
        var radius = km is { } k && CollabFilters.Distances.Contains(k) ? k : CollabLimits.DefaultNearKm;

        var filters = new CollabFilters(list, role, terms, q, near, radius);
        var collabs = await api.GetCollabsAsync(list, role, terms, q, list == CollabLists.Open ? near : null, radius, Math.Max(1, page), ct);
        return View(new CollabsPageViewModel(filters, collabs, places));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var collab = await api.GetCollabAsync(id, ct);
        if (collab is null) return NotFound();
        var messages = collab.CanReadChat ? await api.GetCollabMessagesAsync(id, null, ct) ?? [] : [];
        return View(new CollabPageViewModel(collab, messages));
    }

    [HttpGet]
    public async Task<IActionResult> New(CancellationToken ct) => await FormAsync(new CollabFormViewModel(), ct);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> New(CollabFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await FormAsync(model, ct);
        var result = await api.CreateCollabAsync(model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return await FormAsync(model, ct);
        }
        TempData["Flash"] = result.Value!.ReviewStatus == CollabReviewStatus.Approved
            ? "Your call is on the board. Members who follow you will hear about it."
            : "Thanks! Your call goes on the board once an admin has had a look. We'll let you know.";
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var collab = await api.GetCollabAsync(id, ct);
        if (collab is null || !collab.Collab.IsPoster) return NotFound();
        return await FormAsync(CollabFormViewModel.From(collab), ct, collab.MeetingPoint);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CollabFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return await FormAsync(model, ct);
        var result = await api.UpdateCollabAsync(id, model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return await FormAsync(model, ct);
        }
        TempData["Flash"] = "Call saved. If the time or meeting point changed, the people you accepted have been told.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.CancelCollabAsync(id, ct)
            ? "Collab cancelled. Everyone who replied has been told."
            : "That call can't be cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.DeleteCollabAsync(id, ct) ? "Call deleted." : "That call can't be deleted.";
        return RedirectToAction(nameof(Index), new { list = CollabLists.Mine });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(Guid id, string? message, bool sharePortfolio, CancellationToken ct)
    {
        TempData["Flash"] = string.IsNullOrWhiteSpace(message)
            ? "Write a few words about yourself first."
            : await api.ReplyToCollabAsync(id, new ReplyToCollabRequest(message.Trim(), sharePortfolio), ct)
              ?? "Reply sent. If you're accepted, you'll get the meeting point and a private chat.";
        return Redirect(Url.Action(nameof(Details), new { id }) + "#reply");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.WithdrawCollabReplyAsync(id, ct) ? "Your reply was withdrawn." : "That call is no longer available.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(Guid id, Guid userId, CancellationToken ct)
    {
        TempData["Flash"] = await api.AcceptCollabReplyAsync(id, userId, ct)
                            ?? "Accepted. They now see the meeting point and can talk to you in the chat.";
        return Redirect(Url.Action(nameof(Details), new { id }) + "#replies");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(Guid id, Guid userId, CancellationToken ct)
    {
        TempData["Flash"] = await api.DeclineCollabReplyAsync(id, userId, ct) ?? "Reply declined.";
        return Redirect(Url.Action(nameof(Details), new { id }) + "#replies");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(Guid id, Guid userId, CancellationToken ct)
    {
        TempData["Flash"] = await api.ConfirmCollabAsync(id, userId, ct)
                            ?? "Thanks! Once you've both confirmed, it counts on both your profiles.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>New chat messages since <paramref name="after"/>, as JSON for workshops.js (the same chat).</summary>
    [HttpGet]
    public async Task<IActionResult> Messages(Guid id, long? after, CancellationToken ct)
    {
        var messages = await api.GetCollabMessagesAsync(id, after, ct);
        return messages is null ? NotFound() : Json(messages);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(Guid id, string? body, CancellationToken ct)
    {
        var result = string.IsNullOrWhiteSpace(body)
            ? ApiResult<CollabMessageDto>.Failure("Write something first.")
            : await api.PostCollabMessageAsync(id, body, ct);

        if (Request.IsFetchRequest())
            return result.Succeeded
                ? Json(result.Value)
                : BadRequest(new { error = result.Errors.Values.SelectMany(m => m).FirstOrDefault() });
        if (!result.Succeeded) TempData["Flash"] = result.Errors.Values.SelectMany(m => m).FirstOrDefault();
        return Redirect(Url.Action(nameof(Details), new { id }) + "#chat");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage(Guid id, long messageId, CancellationToken ct)
    {
        var deleted = await api.DeleteCollabMessageAsync(id, messageId, ct);
        if (Request.IsFetchRequest()) return deleted ? NoContent() : NotFound();
        return Redirect(Url.Action(nameof(Details), new { id }) + "#chat");
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

        var outcome = await api.ReportCollabAsync(id, new CreateReportRequest(model.Reason!, model.Note), ct);
        TempData["Flash"] = outcome switch
        {
            ReportOutcome.Reported => "Thanks for letting us know. We'll take a look.",
            ReportOutcome.AlreadyReported => "You've already reported this call.",
            ReportOutcome.NotAllowed => "You can't report your own call.",
            _ => "That call is no longer available.",
        };
        return outcome == ReportOutcome.NotFound ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { id });
    }

    private async Task<ReportViewModel?> ReportModelAsync(ReportViewModel model, CancellationToken ct)
    {
        var collab = await api.GetCollabAsync(model.Id, ct);
        if (collab is null) return null;
        model.Subject = "collab call";
        model.Title = collab.Collab.Title;
        model.ThumbnailUrl = collab.Collab.CoverUrl;
        model.QuotedText = collab.Description;
        model.CancelUrl = Url.Action(nameof(Details), new { id = model.Id });
        return model;
    }

    /// <param name="meetingPoint">When editing: the call's meeting point, to pick the place it came from.</param>
    private async Task<IActionResult> FormAsync(CollabFormViewModel model, CancellationToken ct, CollabMeetingPointDto? meetingPoint = null)
    {
        model.Places = await api.GetPlacesAsync(ct);
        if (meetingPoint is not null && model.PlaceId is null)
            model.PlaceId = model.Places.FirstOrDefault(p => p.Latitude == meetingPoint.Latitude && p.Longitude == meetingPoint.Longitude)?.Id;
        // The member's approved photos that everyone can see: the same list the portfolio offers.
        model.Candidates = (await api.GetMyPortfolioAsync(ct))?.Candidates ?? [];
        return View("Form", model);
    }
}
