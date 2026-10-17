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

        var result = await api.UploadPhotoAsync(model.Photo!, model.Title, model.Description, ct);
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
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditPhotoViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await api.UpdatePhotoAsync(id, new UpdatePhotoRequest(model.Title, model.Description), ct);
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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken ct)
    {
        var deleted = await api.DeleteCommentAsync(id, commentId, ct);
        if (Request.IsFetchRequest()) return deleted ? NoContent() : NotFound();
        return Redirect(Url.Action(nameof(Details), new { id }) + "#comments");
    }
}
