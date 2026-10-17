using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Services.Moderation;

/// <summary>What a report is about. Exactly one id is set, matching the reports table's check constraint.</summary>
public sealed record ReportTarget(string Kind, Guid Id, Guid? PhotoId = null, Guid? CommentId = null,
    Guid? BlogPostId = null, Guid? BlogCommentId = null)
{
    public static ReportTarget Photo(Guid id) => new("Photo", id, PhotoId: id);
    public static ReportTarget PhotoComment(Guid id) => new("Comment", id, CommentId: id);
    public static ReportTarget BlogPost(Guid id) => new("Blog post", id, BlogPostId: id);
    public static ReportTarget BlogComment(Guid id) => new("Blog comment", id, BlogCommentId: id);
}

public enum ReportResult { Saved, InvalidReason, AlreadyReported }

/// <summary>Records reports (one per member per item) and logs when an item crosses the auto-hide threshold.</summary>
public sealed class ReportService(PhotoWorldDbContext db, TimeProvider clock, ILogger<ReportService> logger)
{
    public async Task<ReportResult> SaveAsync(ReportTarget target, Guid reporterId, CreateReportRequest request, CancellationToken ct)
    {
        if (!ReportRules.Reasons.ContainsKey(request.Reason)) return ReportResult.InvalidReason;

        var onTarget = db.Reports.Where(r => r.PhotoId == target.PhotoId && r.CommentId == target.CommentId
            && r.BlogPostId == target.BlogPostId && r.BlogCommentId == target.BlogCommentId);
        if (await onTarget.AnyAsync(r => r.ReporterId == reporterId, ct)) return ReportResult.AlreadyReported;

        db.Reports.Add(new Report
        {
            PhotoId = target.PhotoId,
            CommentId = target.CommentId,
            BlogPostId = target.BlogPostId,
            BlogCommentId = target.BlogCommentId,
            ReporterId = reporterId,
            Reason = request.Reason,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = clock.GetUtcNow(),
        });
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
        return ReportResult.Saved;
    }
}
