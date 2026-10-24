using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Web.Models;

public sealed record BlogPageViewModel(BlogDto Blog, PagedResult<BlogPostSummaryDto> Posts);

/// <summary>The blogs page: everyone's posts, or only those by members the reader follows.</summary>
public sealed record BlogsIndexViewModel(PagedResult<BlogPostSummaryDto> Posts, bool Following);

public sealed record PostPageViewModel(BlogPostDto Post, IReadOnlyList<BlogCommentDto> Comments);

/// <summary>An admin's Approve / Reject buttons for a pending post; <see cref="ReturnUrl"/> is where they go back to.</summary>
public sealed record ReviewActionsModel(Guid PostId, string Title, string ReturnUrl);

public sealed record BlogCommentItemViewModel(Guid PostId, BlogCommentDto Comment);

public sealed class PostEditorViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Give your post a title.")]
    [StringLength(BlogLimits.PostTitleMax)]
    public string Title { get; set; } = "";

    /// <summary>Filled from the editor on submit (Quill's semantic HTML); cleaned by the API.</summary>
    public string BodyHtml { get; set; } = "";

    public bool Publish { get; set; }

    /// <summary>The photo on the post's tile; empty means the first photo in the post.</summary>
    public Guid? CoverPhotoId { get; set; }

    /// <summary>Preview of the chosen cover in the editor (display only; the API checks <see cref="CoverPhotoId"/>).</summary>
    public string? CoverUrl { get; set; }

    /// <summary>Current status when editing, to label the buttons.</summary>
    public string? Status { get; set; }

    /// <summary>Why an admin sent the post back, shown above the form while it's rejected (display only).</summary>
    [BindNever]
    public string? ReviewNote { get; set; }

    /// <summary>PDFs chosen in the form, uploaded once the post is saved.</summary>
    public List<IFormFile> NewAttachments { get; set; } = [];

    /// <summary>Existing PDFs the author ticked for removal.</summary>
    public List<Guid> RemoveAttachmentIds { get; set; } = [];

    /// <summary>The post's current PDFs when editing (loaded from the API, never bound from the form).</summary>
    [BindNever]
    public IReadOnlyList<BlogAttachmentDto> Attachments { get; set; } = [];
}

public sealed class BlogSettingsViewModel
{
    [Required(ErrorMessage = "Give your blog a title.")]
    [StringLength(BlogLimits.BlogTitleMax)]
    public string Title { get; set; } = "";

    [StringLength(BlogLimits.IntroMax)]
    public string? Intro { get; set; }
}
