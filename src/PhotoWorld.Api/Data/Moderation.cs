using System.Linq.Expressions;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Data;

/// <summary>
/// The single definition of "hidden" (photos, comments, blog posts and blog comments): an upheld report, or <see cref="ReportRules.AutoHideThreshold"/> or more
/// open reports. Written as expressions so EF translates them to SQL; tests compile them to check the rule.
/// </summary>
public static class Moderation
{
    public static Expression<Func<Photo, bool>> PhotoVisibleTo(Guid? userId) => p =>
        p.OwnerId == userId
        || !(p.Reports.Any(r => r.Status == ReportStatus.Upheld)
             || p.Reports.Count(r => r.Status == ReportStatus.Open) >= ReportRules.AutoHideThreshold);

    public static Expression<Func<PhotoComment, bool>> CommentVisibleTo(Guid? userId) => c =>
        c.AuthorId == userId
        || !(c.Reports.Any(r => r.Status == ReportStatus.Upheld)
             || c.Reports.Count(r => r.Status == ReportStatus.Open) >= ReportRules.AutoHideThreshold);

    /// <summary>Published, not hidden by moderation; authors always see their own posts, drafts included.</summary>
    public static Expression<Func<BlogPost, bool>> PostVisibleTo(Guid? userId) => p =>
        p.AuthorId == userId
        || (p.Status == BlogPostStatus.Published
            && !(p.Reports.Any(r => r.Status == ReportStatus.Upheld)
                 || p.Reports.Count(r => r.Status == ReportStatus.Open) >= ReportRules.AutoHideThreshold));

    public static Expression<Func<BlogComment, bool>> BlogCommentVisibleTo(Guid? userId) => c =>
        c.AuthorId == userId
        || !(c.Reports.Any(r => r.Status == ReportStatus.Upheld)
             || c.Reports.Count(r => r.Status == ReportStatus.Open) >= ReportRules.AutoHideThreshold);

    /// <summary>The state shown to a content owner, from the same two facts the visibility rule uses.</summary>
    public static ModerationState State(bool anyUpheld, int openReports) =>
        anyUpheld ? ModerationState.Removed
        : openReports >= ReportRules.AutoHideThreshold ? ModerationState.PendingReview
        : ModerationState.Visible;
}
