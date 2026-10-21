using System.ComponentModel.DataAnnotations;

namespace PhotoWorld.Shared.Photos;

public sealed record UserSummaryDto(Guid Id, string DisplayName, string? AvatarUrl);

public sealed record PhotoSummaryDto(
    Guid Id,
    string Title,
    string ThumbnailUrl,
    int Width,
    int Height,
    UserSummaryDto Owner,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    ModerationState Moderation = ModerationState.Visible);

/// <summary>Settings read from the photo's EXIF data when it was uploaded. Any of them may be missing.</summary>
public sealed record CameraDetailsDto(
    string? Make,
    string? Model,
    string? Lens,
    decimal? FocalLengthMm,
    decimal? Aperture,
    string? ExposureTime,
    int? Iso,
    DateTimeOffset? TakenAt)
{
    public bool IsEmpty => Make is null && Model is null && Lens is null && FocalLengthMm is null
        && Aperture is null && ExposureTime is null && Iso is null && TakenAt is null;
}

public sealed record PhotoDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string ImageUrl,
    string ThumbnailUrl,
    int Width,
    int Height,
    UserSummaryDto Owner,
    CameraDetailsDto Camera,
    int LikeCount,
    int CommentCount,
    bool LikedByMe,
    bool CanEdit,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ModerationState Moderation = ModerationState.Visible,
    bool CanReport = false,
    bool ShowOnHome = false,
    PhotoPlaceDto? Place = null,
    IReadOnlyList<string>? Tags = null,
    DateOnly? PhotoOfTheDay = null);

/// <summary>Where a photo was taken: one of its owner's places, which they chose to show with it.</summary>
public sealed record PhotoPlaceDto(Guid Id, string Name, double Latitude, double Longitude);

/// <summary>A photo on the Spots map. Only the linked place's name and position are shared, never its notes or date.</summary>
public sealed record PhotoSpotDto(
    Guid PhotoId,
    string Title,
    string ThumbnailUrl,
    Guid OwnerId,
    string OwnerName,
    string PlaceName,
    double Latitude,
    double Longitude);

/// <summary>A photo its owner chose to feature in the public home-page slideshow.</summary>
public sealed record ShowcasePhotoDto(Guid Id, string Title, string ImageUrl, int Width, int Height, string OwnerName);

public sealed record CommentDto(
    Guid Id,
    UserSummaryDto Author,
    string Text,
    DateTimeOffset CreatedAt,
    bool CanDelete,
    ModerationState Moderation = ModerationState.Visible,
    bool CanReport = false);

public sealed record LikeStateDto(bool LikedByMe, int LikeCount);

public sealed record MemberProfileDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    DateTimeOffset MemberSince,
    int PhotoCount,
    bool IsMe,
    int FollowerCount = 0,
    int FollowingCount = 0,
    bool FollowedByMe = false,
    IReadOnlyList<ChallengeWinDto>? Wins = null,
    bool BlockedByMe = false);

public sealed record FollowStateDto(bool FollowedByMe, int FollowerCount);

/// <summary>A photo challenge the member won, for the trophies on their page.</summary>
public sealed record ChallengeWinDto(Guid ChallengeId, string Title, DateTimeOffset EndsAt, Guid PhotoId);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed record UpdatePhotoRequest(
    [Required, StringLength(PhotoUploadLimits.MaxTitleLength)] string Title,
    [StringLength(PhotoUploadLimits.MaxDescriptionLength)] string? Description,
    bool ShowOnHome = false,
    Guid? PlaceId = null,
    [StringLength(600)] string? Tags = null);

public sealed record CreateCommentRequest(
    [Required, StringLength(PhotoUploadLimits.MaxCommentLength, MinimumLength = 1)] string Text);
