using System.ComponentModel.DataAnnotations;

namespace PhotoWorld.Shared.Photos;

public static class FeedbackLimits
{
    public const int CommentMax = 1000;
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public static readonly IReadOnlyList<(string Key, string Label, string Hint)> Criteria =
    [
        ("composition", "Composition", "Framing, balance, where the eye goes"),
        ("light", "Light", "Exposure, mood, use of the light"),
        ("focus", "Focus", "Sharpness where it matters, depth of field"),
    ];
}

/// <summary>One member's feedback on a photo; private between them and the photo's owner.</summary>
public sealed record FeedbackDto(
    Guid Id,
    UserSummaryDto Author,
    int Composition,
    int Light,
    int Focus,
    string? Comment,
    DateTimeOffset UpdatedAt,
    bool IsMine);

public sealed record FeedbackAveragesDto(double Composition, double Light, double Focus);

/// <summary>
/// The feedback panel on a photo. The owner gets every item and the averages; anyone else gets only the count and their
/// own feedback (<see cref="Mine"/>), and <see cref="CanGive"/> while the owner is asking for it.
/// </summary>
public sealed record PhotoFeedbackDto(
    bool WantsFeedback,
    int Count,
    FeedbackAveragesDto? Averages,
    IReadOnlyList<FeedbackDto> Items,
    FeedbackDto? Mine,
    bool CanGive);

public sealed record SaveFeedbackRequest(
    [Range(FeedbackLimits.MinRating, FeedbackLimits.MaxRating)] int Composition,
    [Range(FeedbackLimits.MinRating, FeedbackLimits.MaxRating)] int Light,
    [Range(FeedbackLimits.MinRating, FeedbackLimits.MaxRating)] int Focus,
    [StringLength(FeedbackLimits.CommentMax)] string? Comment);
