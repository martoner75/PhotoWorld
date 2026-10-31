using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services.Notifications;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Services.Moderation;

/// <summary>What a report is about. Exactly one id is set, matching the reports table's check constraint.</summary>
public sealed record ReportTarget(string Kind, Guid Id, Guid? PhotoId = null, Guid? CommentId = null,
    Guid? BlogPostId = null, Guid? BlogCommentId = null, Guid? ClubId = null, Guid? CollabId = null)
{
    public static ReportTarget Photo(Guid id) => new("Photo", id, PhotoId: id);
    public static ReportTarget PhotoComment(Guid id) => new("Comment", id, CommentId: id);
    public static ReportTarget BlogPost(Guid id) => new("Blog post", id, BlogPostId: id);
    public static ReportTarget BlogComment(Guid id) => new("Blog comment", id, BlogCommentId: id);
    public static ReportTarget Club(Guid id) => new("Club", id, ClubId: id);
    public static ReportTarget Collab(Guid id) => new("Collab", id, CollabId: id);
}

public enum ReportResult { Saved, InvalidReason, AlreadyReported }

/// <summary>
/// Records reports (one per member per item), logs when an item crosses the auto-hide threshold, and emails the site
/// admins about the first report on an item and again when it is auto-hidden (not every report, to keep it quiet).
/// </summary>
public sealed class ReportService(
    PhotoWorldDbContext db,
    TimeProvider clock,
    IEmailSender sender,
    NotificationEmails emails,
    ILogger<ReportService> logger)
{
    public async Task<ReportResult> SaveAsync(ReportTarget target, Guid reporterId, CreateReportRequest request, CancellationToken ct)
    {
        if (!ReportRules.Reasons.ContainsKey(request.Reason)) return ReportResult.InvalidReason;

        var onTarget = db.Reports.Where(r => r.PhotoId == target.PhotoId && r.CommentId == target.CommentId
            && r.BlogPostId == target.BlogPostId && r.BlogCommentId == target.BlogCommentId && r.ClubId == target.ClubId
            && r.CollabId == target.CollabId);
        if (await onTarget.AnyAsync(r => r.ReporterId == reporterId, ct)) return ReportResult.AlreadyReported;

        var report = new Report
        {
            PhotoId = target.PhotoId,
            CommentId = target.CommentId,
            BlogPostId = target.BlogPostId,
            BlogCommentId = target.BlogCommentId,
            ClubId = target.ClubId,
            CollabId = target.CollabId,
            ReporterId = reporterId,
            Reason = request.Reason,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = clock.GetUtcNow(),
        };
        db.Reports.Add(report);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two reports from the same member at the same moment: the unique index kept just one.
            return ReportResult.AlreadyReported;
        }

        logger.LogInformation("{Kind} {TargetId} reported for {Reason} by {ReporterId}", target.Kind, target.Id, request.Reason, reporterId);
        var open = await onTarget.CountAsync(r => r.Status == ReportStatus.Open, ct);
        if (open == ReportRules.AutoHideThreshold)
            logger.LogWarning("{Kind} {TargetId} auto-hidden after {Count} reports, pending review", target.Kind, target.Id, open);

        var autoHidden = open == ReportRules.AutoHideThreshold;
        if (open == 1 || autoHidden) await AlertAdminsAsync(target, report, open, autoHidden, ct);
        return ReportResult.Saved;
    }

    /// <summary>Best effort: the report is already saved, so a mail problem is logged and never fails the member's request.</summary>
    private async Task AlertAdminsAsync(ReportTarget target, Report report, int open, bool autoHidden, CancellationToken ct)
    {
        try
        {
            var admins = await db.Users
                .Where(u => u.IsAdmin && u.SuspendedAt == null && u.Email != null)
                .Select(u => u.Email!)
                .ToListAsync(ct);
            var reason = ReportRules.Reasons.GetValueOrDefault(report.Reason, report.Reason);
            foreach (var to in admins)
            {
                try
                {
                    await sender.SendAsync(emails.ReportAlert(to, target.Kind, reason, report.Note, open, autoHidden, report.Id), ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Could not email an admin about a report on {Kind} {TargetId}", target.Kind, target.Id);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not look up admins to alert about a report on {Kind} {TargetId}", target.Kind, target.Id);
        }
    }
}
