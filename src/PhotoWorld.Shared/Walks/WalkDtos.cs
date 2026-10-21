using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Shared.Walks;

public static class WalkLimits
{
    public const int TitleMax = 100;
    public const int DescriptionMax = 2000;
    public const int MeetingPointMax = 150;
    public const int MaxPeopleLimit = 200;
    public static readonly TimeSpan MaxLength = TimeSpan.FromHours(12);

    /// <summary>The tag members put on photos from a walk: "walk-" and the start of its id.</summary>
    public static string TagFor(Guid walkId) => "walk-" + walkId.ToString("N")[..8];
}

/// <summary>Which walks to list.</summary>
public static class WalkLists
{
    public const string Upcoming = "upcoming";
    public const string Past = "past";
    /// <summary>Walks the caller organises or is going to (upcoming and past).</summary>
    public const string Mine = "mine";
}

public sealed record WalkSummaryDto(
    Guid Id,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string MeetingPoint,
    double Latitude,
    double Longitude,
    UserSummaryDto Organizer,
    int AttendeeCount,
    int? MaxPeople,
    bool IsGoing,
    bool IsOrganizer,
    bool IsCancelled,
    string Tag,
    string? CoverUrl);

/// <summary>A walk's page: the plan, who's going, and the photos tagged with its tag.</summary>
public sealed record WalkDto(
    WalkSummaryDto Walk,
    string? Description,
    IReadOnlyList<UserSummaryDto> Attendees,
    IReadOnlyList<PhotoSummaryDto> Photos,
    int PhotoCount,
    bool CanEdit,
    bool CanJoin,
    string? CannotJoinReason);

public sealed record SaveWalkRequest(
    [Required, StringLength(WalkLimits.TitleMax)] string Title,
    [StringLength(WalkLimits.DescriptionMax)] string? Description,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    [Required, StringLength(WalkLimits.MeetingPointMax)] string MeetingPoint,
    [Range(-90.0, 90.0)] double Latitude,
    [Range(-180.0, 180.0)] double Longitude,
    [Range(2, WalkLimits.MaxPeopleLimit)] int? MaxPeople);
