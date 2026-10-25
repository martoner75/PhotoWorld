using System.Linq.Expressions;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Data;

/// <summary>
/// The single definition of "hidden" (photos, comments, blog posts and blog comments): an upheld report, or <see cref="ReportRules.AutoHideThreshold"/> or more
/// open reports, hides content from everyone but its owner; content by a suspended member is hidden from everyone; and a
/// viewer never sees content by a member they blocked.
/// Written as expressions so EF translates them to SQL; tests compile them to check the rule.
/// </summary>
public static class Moderation
{
    /// <summary>
    /// Approved and not hidden by moderation; owners always see their own photos, including ones waiting for (or
    /// refused) approval. A <paramref name="reviewer"/> (an admin) also sees photos waiting for approval.
    /// </summary>
    public static Expression<Func<Photo, bool>> PhotoVisibleTo(Guid? userId, bool reviewer = false) => p =>
        p.OwnerId == userId
        || (reviewer && p.ReviewStatus == PhotoReviewStatus.Pending)
        || (p.ReviewStatus == PhotoReviewStatus.Approved
            && !(p.Reports.Any(r => r.Status == ReportStatus.Upheld)
              || p.Reports.Count(r => r.Status == ReportStatus.Open) >= ReportRules.AutoHideThreshold)
            && p.Owner.SuspendedAt == null
            && !p.Owner.BlockedBy.Any(b => b.BlockerId == userId));

    public static Expression<Func<PhotoComment, bool>> CommentVisibleTo(Guid? userId) => c =>
        c.AuthorId == userId
        || (!(c.Reports.Any(r => r.Status == ReportStatus.Upheld)
              || c.Reports.Count(r => r.Status == ReportStatus.Open) >= ReportRules.AutoHideThreshold)
            && c.Author.SuspendedAt == null
            && !c.Author.BlockedBy.Any(b => b.BlockerId == userId));

    /// <summary>
    /// Published, not hidden by moderation; authors always see their own posts, drafts included. A
    /// <paramref name="reviewer"/> (an admin) also sees posts waiting for approval.
    /// </summary>
    public static Expression<Func<BlogPost, bool>> PostVisibleTo(Guid? userId, bool reviewer = false) => p =>
        p.AuthorId == userId
        || (reviewer && p.Status == BlogPostStatus.Pending)
        || (p.Status == BlogPostStatus.Published
            && !(p.Reports.Any(r => r.Status == ReportStatus.Upheld)
                 || p.Reports.Count(r => r.Status == ReportStatus.Open) >= ReportRules.AutoHideThreshold)
            && p.Author.SuspendedAt == null
            && !p.Author.BlockedBy.Any(b => b.BlockerId == userId));

    public static Expression<Func<BlogComment, bool>> BlogCommentVisibleTo(Guid? userId) => c =>
        c.AuthorId == userId
        || (!(c.Reports.Any(r => r.Status == ReportStatus.Upheld)
              || c.Reports.Count(r => r.Status == ReportStatus.Open) >= ReportRules.AutoHideThreshold)
            && c.Author.SuspendedAt == null
            && !c.Author.BlockedBy.Any(b => b.BlockerId == userId));

    /// <summary>Their own collections, and other members' public ones (not by suspended or blocked members).</summary>
    public static Expression<Func<Collection, bool>> CollectionVisibleTo(Guid? userId) => c =>
        c.OwnerId == userId
        || (c.IsPublic && c.Owner.SuspendedAt == null && !c.Owner.BlockedBy.Any(b => b.BlockerId == userId));

    /// <summary>
    /// Approved and not hidden by moderation (reports, or a suspended or blocked owner); owners always see their own
    /// club, whatever its state. A <paramref name="reviewer"/> (a site admin) also sees clubs waiting for approval.
    /// </summary>
    public static Expression<Func<Club, bool>> ClubVisibleTo(Guid? userId, bool reviewer = false) => c =>
        c.OwnerId == userId
        || (reviewer && c.ReviewStatus == ClubReviewStatus.Pending)
        || (c.ReviewStatus == ClubReviewStatus.Approved
            && !(c.Reports.Any(r => r.Status == ReportStatus.Upheld)
                 || c.Reports.Count(r => r.Status == ReportStatus.Open) >= ReportRules.AutoHideThreshold)
            && c.Owner.SuspendedAt == null
            && !c.Owner.BlockedBy.Any(b => b.BlockerId == userId));

    /// <summary>The state shown to a content owner, from the same two facts the visibility rule uses.</summary>
    public static ModerationState State(bool anyUpheld, int openReports) =>
        anyUpheld ? ModerationState.Removed
        : openReports >= ReportRules.AutoHideThreshold ? ModerationState.PendingReview
        : ModerationState.Visible;
}
