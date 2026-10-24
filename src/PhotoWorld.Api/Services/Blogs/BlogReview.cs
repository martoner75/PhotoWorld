using PhotoWorld.Shared.Blogs;

namespace PhotoWorld.Api.Services.Blogs;

/// <summary>The approval rules for blog posts (see <see cref="BlogPostStatus"/>), kept apart so they're easy to test.</summary>
public static class BlogReview
{
    /// <summary>
    /// The status a post gets when its author saves it. Saving without publishing makes a draft. Publishing sends it to
    /// the admins' queue, except for admins' own posts and posts already approved, which stay (or go straight) live.
    /// </summary>
    public static string StatusAfterSave(string? current, bool publish, bool authorIsAdmin) =>
        !publish ? BlogPostStatus.Draft
        : current == BlogPostStatus.Published || authorIsAdmin ? BlogPostStatus.Published
        : BlogPostStatus.Pending;

    /// <summary>Only posts waiting in the queue can be approved or rejected.</summary>
    public static bool CanReview(string status) => status == BlogPostStatus.Pending;
}
