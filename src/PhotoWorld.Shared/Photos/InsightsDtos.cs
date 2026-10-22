namespace PhotoWorld.Shared.Photos;

/// <summary>A number for the last 30 days, and the 30 days before, for the "vs previous 30 days" change.</summary>
public sealed record InsightStatDto(int Current, int Previous);

public sealed record InsightDayDto(DateOnly Date, int Views, int Likes);

public sealed record InsightPhotoDto(PhotoSummaryDto Photo, int Views, int Likes);

public sealed record InsightTagDto(string Tag, int Likes, int Photos);

/// <summary>
/// A member's insights: views (photo pages opened by other members), likes, new followers and comments over the last
/// 30 days, per day, their best photos and tags. Views are counted per photo per day; nobody is recorded.
/// </summary>
public sealed record InsightsDto(
    InsightStatDto Views,
    InsightStatDto Likes,
    InsightStatDto NewFollowers,
    InsightStatDto Comments,
    int Followers,
    int Photos,
    IReadOnlyList<InsightDayDto> Days,
    IReadOnlyList<InsightPhotoDto> TopPhotos,
    IReadOnlyList<InsightTagDto> TopTags);
