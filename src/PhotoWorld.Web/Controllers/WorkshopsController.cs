using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Workshops;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class WorkshopsController(IPhotoWorldApiClient api) : Controller
{
    private static readonly Dictionary<string, string> FormFields = new()
    {
        ["title"] = nameof(WorkshopFormViewModel.Title),
        ["startsAt"] = nameof(WorkshopFormViewModel.StartsLocal),
        ["endsAt"] = nameof(WorkshopFormViewModel.DurationMinutes),
        ["videoLink"] = nameof(WorkshopFormViewModel.VideoLink),
        ["maxPeople"] = nameof(WorkshopFormViewModel.MaxPeople),
    };

    [HttpGet]
    public async Task<IActionResult> Index(string? list, CancellationToken ct)
    {
        list = list is WorkshopLists.Past or WorkshopLists.Mine ? list : WorkshopLists.Upcoming;
        return View(new WorkshopsPageViewModel(list, await api.GetWorkshopsAsync(list, ct)));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var workshop = await api.GetWorkshopAsync(id, ct);
        if (workshop is null) return NotFound();
        var messages = workshop.CanReadChat ? await api.GetWorkshopMessagesAsync(id, null, ct) ?? [] : [];
        return View(new WorkshopPageViewModel(workshop, messages));
    }

    /// <summary>A new workshop; with <paramref name="clubId"/> (from a club's page) it starts as that club's workshop.</summary>
    [HttpGet]
    public async Task<IActionResult> New(Guid? clubId, CancellationToken ct) =>
        await FormAsync(new WorkshopFormViewModel { ClubId = clubId }, ct);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> New(WorkshopFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await FormAsync(model, ct);
        var result = await api.CreateWorkshopAsync(model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return await FormAsync(model, ct);
        }
        TempData["Flash"] = model.ClubId is null
            ? "Your workshop is on. Members who follow you will hear about it."
            : "Your club workshop is on. The club's members will hear about it; only they can see it.";
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var workshop = await api.GetWorkshopAsync(id, ct);
        if (workshop is null || !workshop.CanEdit) return NotFound();
        return await FormAsync(WorkshopFormViewModel.From(workshop), ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, WorkshopFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return await FormAsync(model, ct);
        var result = await api.UpdateWorkshopAsync(id, model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return await FormAsync(model, ct);
        }
        TempData["Flash"] = "Workshop saved. If the time or video link changed, everyone going has been told.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.CancelWorkshopAsync(id, ct)
            ? "Workshop cancelled. Everyone going has been told."
            : "That workshop can't be cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.DeleteWorkshopAsync(id, ct) ? "Workshop deleted." : "That workshop can't be deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Attend(Guid id, bool going, CancellationToken ct)
    {
        TempData["Flash"] = await api.SetWorkshopAttendanceAsync(id, going, ct)
            ?? (going ? "You're in! You'll get a reminder the day before, and you can say hello in the chat." : "You're no longer going.");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>New chat messages since <paramref name="after"/>, as JSON for workshops.js.</summary>
    [HttpGet]
    public async Task<IActionResult> Messages(Guid id, long? after, CancellationToken ct)
    {
        var messages = await api.GetWorkshopMessagesAsync(id, after, ct);
        return messages is null ? NotFound() : Json(messages);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(Guid id, string? body, CancellationToken ct)
    {
        var result = string.IsNullOrWhiteSpace(body)
            ? ApiResult<WorkshopMessageDto>.Failure("Write something first.")
            : await api.PostWorkshopMessageAsync(id, body, ct);

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
        var deleted = await api.DeleteWorkshopMessageAsync(id, messageId, ct);
        if (Request.IsFetchRequest()) return deleted ? NoContent() : NotFound();
        return Redirect(Url.Action(nameof(Details), new { id }) + "#chat");
    }

    private async Task<IActionResult> FormAsync(WorkshopFormViewModel model, CancellationToken ct)
    {
        if (model.Id is null)
            model.Clubs = (await api.GetMyClubsAsync(ct))
                .Where(c => c.ReviewStatus == Shared.Clubs.ClubReviewStatus.Approved && c.MyStatus == Shared.Clubs.ClubMemberStatus.Active)
                .ToList();
        return View("Form", model);
    }
}
