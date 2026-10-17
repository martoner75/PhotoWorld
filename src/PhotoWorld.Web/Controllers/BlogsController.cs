using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Web.Authentication;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class BlogsController(IPhotoWorldApiClient api) : Controller
{
    private static readonly Dictionary<string, string> PostFields = new()
    {
        ["title"] = nameof(PostEditorViewModel.Title),
        ["bodyHtml"] = nameof(PostEditorViewModel.BodyHtml),
    };

    /// <summary>The newest published posts from everyone.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(await api.GetPostsAsync(page, ct: ct));

    /// <summary>One member's blog.</summary>
    [HttpGet]
    public async Task<IActionResult> Member(Guid id, int page = 1, CancellationToken ct = default)
    {
        var blog = await api.GetBlogAsync(id, ct);
        if (blog is null) return NotFound();
        return View(new BlogPageViewModel(blog, await api.GetPostsAsync(page, id, ct)));
    }

    [HttpGet]
    public IActionResult Mine() => RedirectToAction(nameof(Member), new { id = User.GetUserId() });

    [HttpGet]
    public async Task<IActionResult> Post(Guid id, CancellationToken ct)
    {
        var post = await api.GetPostAsync(id, ct);
        if (post is null) return NotFound();
        return View(new PostPageViewModel(post, await api.GetBlogCommentsAsync(id, ct)));
    }

    [HttpGet]
    public IActionResult New() => View("Editor", new PostEditorViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> New(PostEditorViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Editor", model);

        var result = await api.CreatePostAsync(new SavePostRequest(model.Title, model.BodyHtml, model.Publish), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, PostFields);
            return View("Editor", model);
        }
        TempData["Flash"] = model.Publish ? "Your post is published." : "Draft saved. Only you can see it.";
        return RedirectToAction(nameof(Post), new { id = result.Value!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var post = await api.GetPostAsync(id, ct);
        if (post is null) return NotFound();
        if (!post.CanEdit) return Forbid();
        return View("Editor", new PostEditorViewModel
        {
            Id = post.Id, Title = post.Title, BodyHtml = post.BodyHtml, Status = post.Status,
            Publish = post.Status == BlogPostStatus.Published,
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, PostEditorViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Editor", model);

        var result = await api.UpdatePostAsync(id, new SavePostRequest(model.Title, model.BodyHtml, model.Publish), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, PostFields);
            return View("Editor", model);
        }
        TempData["Flash"] = model.Publish ? "Post updated." : "Saved as a draft. Only you can see it.";
        return RedirectToAction(nameof(Post), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await api.DeletePostAsync(id, ct)) return NotFound();
        TempData["Flash"] = "Post deleted.";
        return RedirectToAction(nameof(Mine));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Comment(Guid id, string? text, CancellationToken ct)
    {
        var result = string.IsNullOrWhiteSpace(text)
            ? ApiResult<BlogCommentDto>.Failure("Write something first.")
            : await api.AddBlogCommentAsync(id, text, ct);

        if (Request.IsFetchRequest())
        {
            return result.Succeeded
                ? PartialView("_BlogComment", new BlogCommentItemViewModel(id, result.Value!))
                : BadRequest(new { error = result.Errors.Values.SelectMany(m => m).FirstOrDefault() });
        }
        if (!result.Succeeded) TempData["Flash"] = result.Errors.Values.SelectMany(m => m).FirstOrDefault();
        return Redirect(Url.Action(nameof(Post), new { id }) + "#comments");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken ct)
    {
        var deleted = await api.DeleteBlogCommentAsync(id, commentId, ct);
        if (Request.IsFetchRequest()) return deleted ? NoContent() : NotFound();
        return Redirect(Url.Action(nameof(Post), new { id }) + "#comments");
    }

    [HttpGet]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var blog = await api.GetBlogAsync(User.GetUserId()!.Value, ct);
        if (blog is null) return NotFound();
        return View(new BlogSettingsViewModel { Title = blog.Title, Intro = blog.Intro });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(BlogSettingsViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await api.SaveBlogAsync(User.GetUserId()!.Value, new SaveBlogRequest(model.Title, model.Intro), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, new Dictionary<string, string> { ["title"] = "Title", ["intro"] = "Intro" });
            return View(model);
        }
        TempData["Flash"] = "Blog settings saved.";
        return RedirectToAction(nameof(Mine));
    }

    [HttpGet]
    public async Task<IActionResult> Report(Guid id, Guid? commentId, CancellationToken ct)
    {
        var model = await LoadReportTargetAsync(new ReportViewModel { Id = id, CommentId = commentId }, ct);
        return model is null ? NotFound() : View("Report", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(Guid id, ReportViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            var reloaded = await LoadReportTargetAsync(model, ct);
            return reloaded is null ? NotFound() : View("Report", reloaded);
        }

        var outcome = await api.ReportBlogAsync(id, model.CommentId, new CreateReportRequest(model.Reason!, model.Note), ct);
        TempData["Flash"] = outcome switch
        {
            ReportOutcome.Reported => "Thanks for letting us know. We'll take a look.",
            ReportOutcome.AlreadyReported => "You've already reported this.",
            ReportOutcome.NotAllowed => "You can't report your own posts or comments.",
            _ => "That post or comment is no longer available.",
        };
        if (outcome == ReportOutcome.NotFound) return RedirectToAction(nameof(Index));
        return Redirect(Url.Action(nameof(Post), new { id }) + (model.CommentId is null ? "" : "#comments"));
    }

    // ----- Editor helpers (JSON, called by editor.js) -----

    /// <summary>A page of the signed-in member's photos for the photo picker.</summary>
    [HttpGet]
    public async Task<IActionResult> MyPhotos(int page = 1, CancellationToken ct = default)
    {
        var photos = await api.GetPhotosAsync(page, User.GetUserId(), ct);
        return Json(new
        {
            items = photos.Items.Select(p => new { id = p.Id, title = p.Title, thumbnailUrl = p.ThumbnailUrl }),
            hasNext = photos.HasNext,
        });
    }

    /// <summary>Uploads a photo from the editor; it joins the member's gallery like any other upload.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(PhotoUploadLimits.MaxBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadLimits.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(IFormFile? photo, string? title, CancellationToken ct)
    {
        if (photo is null || photo.Length == 0) return BadRequest(new { error = "Choose a photo to upload." });
        if (string.IsNullOrWhiteSpace(title)) return BadRequest(new { error = "Give your photo a title." });

        var result = await api.UploadPhotoAsync(photo, title.Trim(), null, showOnHome: false, ct);
        return result.Succeeded
            ? Json(new { id = result.Value!.Id, title = result.Value.Title, thumbnailUrl = result.Value.ThumbnailUrl })
            : BadRequest(new { error = result.Errors.Values.SelectMany(m => m).FirstOrDefault() ?? "Upload failed." });
    }

    private async Task<ReportViewModel?> LoadReportTargetAsync(ReportViewModel model, CancellationToken ct)
    {
        var post = await api.GetPostAsync(model.Id, ct);
        if (post is null) return null;
        model.Subject = "post";
        model.Title = post.Title;
        model.ThumbnailUrl = post.CoverUrl;
        model.CancelUrl = Url.Action(nameof(Post), new { id = model.Id });

        if (model.CommentId is { } commentId)
        {
            var comment = (await api.GetBlogCommentsAsync(model.Id, ct)).FirstOrDefault(c => c.Id == commentId);
            if (comment is null) return null;
            model.Subject = "comment";
            model.QuotedText = comment.Text;
        }
        return model;
    }
}
