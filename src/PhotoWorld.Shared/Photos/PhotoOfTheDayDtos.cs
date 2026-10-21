using System.ComponentModel.DataAnnotations;

namespace PhotoWorld.Shared.Photos;

public static class PhotoOfTheDayLimits
{
    public const int NoteMax = 200;
}

/// <summary>The featured photo: today's, or the most recent one while today has none.</summary>
public sealed record PhotoOfTheDayDto(DateOnly Date, PhotoSummaryDto Photo, string? Note, bool IsToday);

/// <summary>One day on the admins' schedule.</summary>
public sealed record PhotoOfTheDayScheduleDto(DateOnly Date, PhotoSummaryDto Photo, string? Note, string? ChosenBy);

public sealed record SetPhotoOfTheDayRequest([Required] Guid PhotoId, [StringLength(PhotoOfTheDayLimits.NoteMax)] string? Note);
