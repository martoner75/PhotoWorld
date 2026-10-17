using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class PhotosController(IPhotoWorldApiClient api) : Controller
{
    private static readonly Dictionary<string, string> UploadFields = new()
    {
        ["file"] = nameof(UploadPhotoViewModel.Photo),
        ["title"] = nameof(UploadPhotoViewModel.Title),
        ["description"] = nameof(UploadPhotoViewModel.Description),
    };

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(await api.GetPhotosAsync(page, ct: ct));

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var photo = await api.GetPhotoAsync(id, ct);
        if (photo is null) return NotFound();
        return View(new PhotoPageViewModel(photo, await api.GetCommentsAsync(id, ct)));
    }

    [HttpGet]
    public IActionResult Upload() => View(new UploadPhotoViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(PhotoUploadLimits.MaxBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadLimits.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(UploadPhotoViewModel model, CancellationToken ct)
    {
        if (model.Photo is { Length: > PhotoUploadLimits.MaxBytes })
            ModelState.AddModelError(nameof(model.Photo), $"Photos can be up to {PhotoUploadLimits.MaxBytes / (1024 * 1024)} MB.");
        if (!ModelState.IsValid) return View(model);

        var result = await api.UploadPhotoAsync(model.Photo!, model.Title, model.Description, model.ShowOnHome, ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, UploadFields);
            return View(model);
        }

        TempData["Flash"] = "Your photo is live. Thanks for sharing!";
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var photo = await api.GetPhotoAsync(id, ct);
        if (photo is null) return NotFound();
        if (!photo.CanEdit) return Forbid();
        return View(new EditPhotoViewModel
        {
            Id = photo.Id, ThumbnailUrl = photo.ThumbnailUrl, Title = photo.Title, Description = photo.Description,
            ShowOnHome = photo.ShowOnHome,
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditPhotoViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await api.UpdatePhotoAsync(id, new UpdatePhotoRequest(model.Title, model.Description, model.ShowOnHome), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, UploadFields);
            return View(model);
        }

        TempData["Flash"] = "Changes saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await api.DeletePhotoAsync(id, ct)) return NotFound();
        TempData["Flash"] = "Photo deleted.";
        return RedirectToAction("Me", "Members");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Like(Guid id, bool liked, CancellationToken ct)
    {
        var state = await api.SetLikeAsync(id, liked, ct);
        if (state is null) return NotFound();
        return Request.IsFetchRequest() ? Json(state) : RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Comment(Guid id, string? text, CancellationToken ct)
    {
        var result = string.IsNullOrWhiteSpace(text)
            ? ApiResult<CommentDto>.Failure("Write something first.")
            : await api.AddCommentAsync(id, text, ct);

        if (Request.IsFetchRequest())
        {
            return result.Succeeded
                ? PartialView("_Comment", new CommentItemViewModel(id, result.Value!))
                : BadRequest(new { error = result.Errors.Values.SelectMany(m => m).FirstOrDefault() });
        }
        if (!result.Succeeded) TempData["Flash"] = result.Errors.Values.SelectMany(m => m).FirstOrDefault();
        return Redirect(Url.Action(nameof(Details), new { id }) + "#comments");
    }

    [HttpGet]
    public async Task<IActionResult> Report(Guid id, Guid? commentId, CancellationToken ct)
    {
        var model = await LoadReportTargetAsync(new ReportViewModel { PhotoId = id, CommentId = commentId }, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(Guid id, ReportViewModel model, CancellationToken ct)
    {
        model.PhotoId = id;
        if (!ModelState.IsValid)
        {
            var reloaded = await LoadReportTargetAsync(model, ct);
            return reloaded is null ? NotFound() : View(reloaded);
        }

        var outcome = await api.ReportAsync(id, model.CommentId, new CreateReportRequest(model.Reason!, model.Note), ct);
        TempData["Flash"] = outcome switch
        {
            ReportOutcome.Reported => "Thanks for letting us know. We'll take a look.",
            ReportOutcome.AlreadyReported => "You've already reported this.",
            ReportOutcome.NotAllowed => "You can't report your own photos or comments.",
            _ => "That photo or comment is no longer available.",
        };
        if (outcome == ReportOutcome.NotFound) return RedirectToAction(nameof(Index));
        return Redirect(Url.Action(nameof(Details), new { id }) + (model.CommentId is null ? "" : "#comments"));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken ct)
    {
        var deleted = await api.DeleteCommentAsync(id, commentId, ct);
        if (Request.IsFetchRequest()) return deleted ? NoContent() : NotFound();
        return Redirect(Url.Action(nameof(Details), new { id }) + "#comments");
    }

    /// <summary>Fills in what's being reported; null if the photo or comment isn't visible any more.</summary>
    private async Task<ReportViewModel?> LoadReportTargetAsync(ReportViewModel model, CancellationToken ct)
    {
        var photo = await api.GetPhotoAsync(model.PhotoId, ct);
        if (photo is null) return null;
        model.PhotoTitle = photo.Title;
        model.ThumbnailUrl = photo.ThumbnailUrl;

        if (model.CommentId is { } commentId)
        {
            var comment = (await api.GetCommentsAsync(model.PhotoId, ct)).FirstOrDefault(c => c.Id == commentId);
            if (comment is null) return null;
            model.CommentText = comment.Text;
        }
        return model;
    }
}
