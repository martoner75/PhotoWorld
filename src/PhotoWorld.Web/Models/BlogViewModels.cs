using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Web.Models;

public sealed record BlogPageViewModel(BlogDto Blog, PagedResult<BlogPostSummaryDto> Posts);

public sealed record PostPageViewModel(BlogPostDto Post, IReadOnlyList<BlogCommentDto> Comments);

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

    /// <summary>Current status when editing, to label the buttons.</summary>
    public string? Status { get; set; }
}

public sealed class BlogSettingsViewModel
{
    [Required(ErrorMessage = "Give your blog a title.")]
    [StringLength(BlogLimits.BlogTitleMax)]
    public string Title { get; set; } = "";

    [StringLength(BlogLimits.IntroMax)]
    public string? Intro { get; set; }
}
