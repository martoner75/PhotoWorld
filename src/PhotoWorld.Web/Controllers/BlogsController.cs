using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Web.Authentication;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class BlogsController(IPhotoWorldApiClient api) : Controller
{
    private const long AttachmentsRequestLimit = BlogLimits.AttachmentsMax * BlogLimits.AttachmentMaxBytes + 2 * 1024 * 1024;

    private static readonly Dictionary<string, string> PostFields = new()
    {
        ["title"] = nameof(PostEditorViewModel.Title),
        ["bodyHtml"] = nameof(PostEditorViewModel.BodyHtml),
        ["coverPhotoId"] = nameof(PostEditorViewModel.CoverPhotoId),
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
    [RequestSizeLimit(AttachmentsRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = AttachmentsRequestLimit)]
    public async Task<IActionResult> New(PostEditorViewModel model, CancellationToken ct)
    {
        ValidateAttachments(model);
        if (!ModelState.IsValid) return Editor(model);

        var result = await api.CreatePostAsync(new SavePostRequest(model.Title, model.BodyHtml, model.Publish, model.CoverPhotoId), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, PostFields);
            return Editor(model);
        }
        var id = result.Value!.Id;
        TempData["Flash"] = await SaveAttachmentsAsync(id, model, ct)
                            ?? (model.Publish ? "Your post is published." : "Draft saved. Only you can see it.");
        return RedirectToAction(nameof(Post), new { id });
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
            Publish = post.Status == BlogPostStatus.Published, Attachments = post.Attachments,
            CoverPhotoId = post.ChosenCoverPhotoId, CoverUrl = post.ChosenCoverPhotoId is null ? null : post.CoverUrl,
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(AttachmentsRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = AttachmentsRequestLimit)]
    public async Task<IActionResult> Edit(Guid id, PostEditorViewModel model, CancellationToken ct)
    {
        model.Id = id;
        model.Attachments = (await api.GetPostAsync(id, ct))?.Attachments ?? [];
        ValidateAttachments(model);
        if (!ModelState.IsValid) return Editor(model);

        var result = await api.UpdatePostAsync(id, new SavePostRequest(model.Title, model.BodyHtml, model.Publish, model.CoverPhotoId), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, PostFields);
            return Editor(model);
        }
        TempData["Flash"] = await SaveAttachmentsAsync(id, model, ct)
                            ?? (model.Publish ? "Post updated." : "Saved as a draft. Only you can see it.");
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

    /// <summary>Re-shows the editor. Browsers don't keep chosen files across a round trip, so say so.</summary>
    private ViewResult Editor(PostEditorViewModel model)
    {
        if (model.NewAttachments.Count > 0)
            ModelState.AddModelError(nameof(PostEditorViewModel.NewAttachments), "Please choose your PDFs again.");
        return View("Editor", model);
    }

    /// <summary>Checks chosen PDFs before anything is saved, so a bad file doesn't leave a half-saved post.</summary>
    private void ValidateAttachments(PostEditorViewModel model)
    {
        const string field = nameof(PostEditorViewModel.NewAttachments);
        model.NewAttachments.RemoveAll(f => f.Length == 0);
        foreach (var file in model.NewAttachments)
        {
            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(field, $"{file.FileName} isn't a PDF.");
            else if (file.Length > BlogLimits.AttachmentMaxBytes)
                ModelState.AddModelError(field, $"{file.FileName} is too big. PDFs can be up to {BlogLimits.AttachmentMaxBytes / (1024 * 1024)} MB.");
        }
        var kept = model.Attachments.Count(a => !model.RemoveAttachmentIds.Contains(a.Id));
        if (kept + model.NewAttachments.Count > BlogLimits.AttachmentsMax)
            ModelState.AddModelError(field, $"A post can have up to {BlogLimits.AttachmentsMax} PDFs.");
    }

    /// <summary>Removes ticked PDFs, then uploads new ones. Returns a message if any couldn't be attached.</summary>
    private async Task<string?> SaveAttachmentsAsync(Guid postId, PostEditorViewModel model, CancellationToken ct)
    {
        foreach (var attachmentId in model.RemoveAttachmentIds.Intersect(model.Attachments.Select(a => a.Id)))
            await api.DeleteBlogAttachmentAsync(postId, attachmentId, ct);

        var failures = new List<string>();
        foreach (var file in model.NewAttachments)
        {
            var result = await api.AddBlogAttachmentAsync(postId, file, ct);
            if (!result.Succeeded)
                failures.Add(result.Errors.Values.SelectMany(m => m).FirstOrDefault() ?? $"{file.FileName} couldn't be uploaded.");
        }
        return failures.Count == 0 ? null : "Your post is saved, but some PDFs weren't attached: " + string.Join(" ", failures);
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
