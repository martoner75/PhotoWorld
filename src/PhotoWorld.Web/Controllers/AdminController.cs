using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Admin;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Photos;
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

    /// <summary>Blog posts waiting for approval, longest-waiting first.</summary>
    [HttpGet]
    public async Task<IActionResult> Posts(int page = 1, CancellationToken ct = default)
    {
        var posts = await api.GetPendingPostsAsync(page, ct);
        return posts is null ? Forbid() : View(posts);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApprovePost(Guid id, string? returnUrl, CancellationToken ct)
    {
        TempData["Flash"] = await api.ApprovePostAsync(id, ct)
            ?? "Post approved. It's published, and its author and their followers have been told.";
        return BackTo(returnUrl, nameof(Posts));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectPost(Guid id, string? reason, string? returnUrl, CancellationToken ct)
    {
        if (reason?.Length > BlogLimits.ReviewNoteMax) reason = reason[..BlogLimits.ReviewNoteMax];
        TempData["Flash"] = await api.RejectPostAsync(id, reason, ct)
            ?? "Post sent back to its author. It stays hidden until they submit it again.";
        return BackTo(returnUrl, nameof(Posts));
    }

    /// <summary>New clubs waiting for approval, longest-waiting first.</summary>
    [HttpGet]
    public async Task<IActionResult> Clubs(int page = 1, CancellationToken ct = default)
    {
        var clubs = await api.GetPendingClubsAsync(page, ct);
        return clubs is null ? Forbid() : View(clubs);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveClub(Guid id, string? returnUrl, CancellationToken ct)
    {
        TempData["Flash"] = await api.ApproveClubAsync(id, ct) ?? "Club approved. It's in the directory, and its owner has been told.";
        return BackTo(returnUrl, nameof(Clubs));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectClub(Guid id, string? reason, string? returnUrl, CancellationToken ct)
    {
        if (reason?.Length > ClubLimits.ReviewNoteMax) reason = reason[..ClubLimits.ReviewNoteMax];
        TempData["Flash"] = await api.RejectClubAsync(id, reason, ct)
            ?? "Club turned down. Only its owner can see it now, with your reason.";
        return BackTo(returnUrl, nameof(Clubs));
    }

    /// <summary>Collab calls waiting for approval, longest-waiting first.</summary>
    [HttpGet]
    public async Task<IActionResult> Collabs(int page = 1, CancellationToken ct = default)
    {
        var collabs = await api.GetPendingCollabsAsync(page, ct);
        return collabs is null ? Forbid() : View(collabs);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCollab(Guid id, string? returnUrl, CancellationToken ct)
    {
        TempData["Flash"] = await api.ApproveCollabAsync(id, ct) ?? "Call approved. It's on the board, and its poster has been told.";
        return BackTo(returnUrl, nameof(Collabs));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectCollab(Guid id, string? reason, string? returnUrl, CancellationToken ct)
    {
        if (reason?.Length > 500) reason = reason[..500];
        TempData["Flash"] = await api.RejectCollabAsync(id, reason, ct)
            ?? "Call turned down. Only its poster can see it now, with your reason.";
        return BackTo(returnUrl, nameof(Collabs));
    }

    /// <summary>Uploaded photos waiting for approval, longest-waiting first.</summary>
    [HttpGet]
    public async Task<IActionResult> Photos(int page = 1, CancellationToken ct = default)
    {
        var photos = await api.GetPendingPhotosAsync(page, ct);
        return photos is null ? Forbid() : View(photos);
    }

    /// <summary>Approves one photo, or every photo on a page of the queue.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApprovePhotos(List<Guid> ids, string? returnUrl, CancellationToken ct)
    {
        var approved = ids.Count == 0 ? 0 : await api.ApprovePhotosAsync(ids, ct);
        TempData["Flash"] = approved switch
        {
            null => "Those photos couldn't be approved. Are you still an admin?",
            0 => "Nothing to approve: they were already reviewed.",
            1 => "Photo approved. It's in the gallery, and its owner and their followers have been told.",
            var n => $"{n} photos approved. They're in the gallery, and their owners and followers have been told.",
        };
        return BackTo(returnUrl, nameof(Photos));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectPhoto(Guid id, string? reason, string? returnUrl, CancellationToken ct)
    {
        if (reason?.Length > PhotoReviewStatus.NoteMax) reason = reason[..PhotoReviewStatus.NoteMax];
        TempData["Flash"] = await api.RejectPhotoAsync(id, reason, ct)
            ?? "Photo not approved. Only its owner can see it, with your reason.";
        return BackTo(returnUrl, nameof(Photos));
    }

    [HttpGet]
    public async Task<IActionResult> PhotoOfTheDay(CancellationToken ct)
    {
        var schedule = await api.GetPhotoOfTheDayScheduleAsync(ct);
        return schedule is null ? Forbid() : View(schedule);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPhotoOfTheDay(Guid photoId, DateOnly date, string? note, CancellationToken ct)
    {
        var problem = await api.SetPhotoOfTheDayAsync(date, new SetPhotoOfTheDayRequest(photoId, note), ct);
        TempData["Flash"] = problem ?? $"Featured as the photo of the day for {date:d MMMM yyyy}.";
        return problem is null ? RedirectToAction(nameof(PhotoOfTheDay)) : RedirectToAction("Details", "Photos", new { id = photoId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearPhotoOfTheDay(DateOnly date, CancellationToken ct)
    {
        TempData["Flash"] = await api.ClearPhotoOfTheDayAsync(date, ct) ? $"Removed the pick for {date:d MMMM yyyy}." : "Nothing to remove.";
        return RedirectToAction(nameof(PhotoOfTheDay));
    }

    private IActionResult BackTo(string? returnUrl, string fallbackAction = nameof(Users)) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(fallbackAction);
}
