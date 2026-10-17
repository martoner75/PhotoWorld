using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Shared.Blogs;

public static class BlogLimits
{
    public const int BlogTitleMax = 100;
    public const int IntroMax = 500;
    public const int PostTitleMax = 150;
    public const int BodyMax = 100_000;
    public const int CommentMax = 2000;
    public const int ExcerptLength = 280;
}

public static class BlogPostStatus
{
    public const string Draft = "draft";
    public const string Published = "published";
}

/// <summary>A member's blog. Exists for everyone; <see cref="Title"/> falls back to "{name}'s blog".</summary>
public sealed record BlogDto(UserSummaryDto Author, string Title, string? Intro, int PostCount, bool IsMine);

public sealed record BlogPostSummaryDto(
    Guid Id,
    string Title,
    string Excerpt,
    string? CoverUrl,
    UserSummaryDto Author,
    int CommentCount,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt,
    ModerationState Moderation);

public sealed record BlogPostDto(
    Guid Id,
    string Title,
    string BodyHtml,
    string? CoverUrl,
    UserSummaryDto Author,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CommentCount,
    bool CanEdit,
    bool CanReport,
    ModerationState Moderation);

public sealed record BlogCommentDto(
    Guid Id,
    UserSummaryDto Author,
    string Text,
    DateTimeOffset CreatedAt,
    bool CanDelete,
    ModerationState Moderation,
    bool CanReport);

public sealed record SaveBlogRequest(
    [Required, StringLength(BlogLimits.BlogTitleMax)] string Title,
    [StringLength(BlogLimits.IntroMax)] string? Intro);

/// <summary>
/// Create or update a post. <see cref="BodyHtml"/> is cleaned by the API; photos are embedded as
/// <c>&lt;figure class="pw-photo" data-photo-id="…"&gt;</c> and must be the author's own.
/// </summary>
public sealed record SavePostRequest(
    [Required, StringLength(BlogLimits.PostTitleMax)] string Title,
    [Required, StringLength(BlogLimits.BodyMax)] string BodyHtml,
    bool Publish);

public sealed record CreateBlogCommentRequest(
    [Required, StringLength(BlogLimits.CommentMax, MinimumLength = 1)] string Text);
