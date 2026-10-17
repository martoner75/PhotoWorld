using System.ComponentModel.DataAnnotations;

namespace PhotoWorld.Shared.Photos;

/// <summary>Whether a photo or comment is shown. Only the owner ever sees content that isn't <see cref="Visible"/>.</summary>
public enum ModerationState
{
    Visible,
    /// <summary>Hidden automatically after <see cref="ReportRules.AutoHideThreshold"/> open reports.</summary>
    PendingReview,
    /// <summary>A moderator upheld a report.</summary>
    Removed,
}

public static class ReportRules
{
    public const int AutoHideThreshold = 3;
    public const int MaxNoteLength = 500;

    /// <summary>Report reasons: stored value → label shown in the report form.</summary>
    public static readonly IReadOnlyDictionary<string, string> Reasons = new Dictionary<string, string>
    {
        ["spam"] = "Spam or advertising",
        ["nudity"] = "Nudity or sexual content",
        ["harassment"] = "Harassment or hate",
        ["violence"] = "Violence or graphic content",
        ["copyright"] = "Not their photo / copyright",
        ["other"] = "Something else",
    };
}

public sealed record CreateReportRequest(
    [Required] string Reason,
    [StringLength(ReportRules.MaxNoteLength)] string? Note);
