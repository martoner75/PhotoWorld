using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Shared.Workshops;

public static class WorkshopLimits
{
    public const int TitleMax = 100;
    public const int DescriptionMax = 2000;
    public const int VideoLinkMax = 500;
    public const int MessageMax = 1000;
    public const int MaxPeopleLimit = 200;
    public static readonly TimeSpan MaxLength = TimeSpan.FromHours(8);

    /// <summary>The chat stays open this long after the workshop ends, for follow-up questions; then it's read-only.</summary>
    public static readonly TimeSpan ChatOpenAfter = TimeSpan.FromDays(1);

    /// <summary>How many messages a workshop's page loads at first (the newest ones).</summary>
    public const int MessagesShown = 100;

    /// <summary>How often the website and the app ask for new messages while the chat is on screen.</summary>
    public static readonly TimeSpan ChatRefresh = TimeSpan.FromSeconds(5);
}

/// <summary>Which workshops to list.</summary>
public static class WorkshopLists
{
    public const string Upcoming = "upcoming";
    public const string Past = "past";
    /// <summary>Workshops the caller hosts or is going to (upcoming and past).</summary>
    public const string Mine = "mine";
}

public sealed record WorkshopSummaryDto(
    Guid Id,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    UserSummaryDto Host,
    int AttendeeCount,
    int? MaxPeople,
    bool IsGoing,
    bool IsHost,
    bool IsCancelled,
    bool HasVideoLink,
    Guid? ClubId = null,
    string? ClubName = null);

/// <summary>
/// A workshop's page: the topic, who's going and, for the host and people going, the video link and whether they can chat.
/// </summary>
/// <param name="VideoLink">Only for the host and people going; null for everyone else, or when there is none.</param>
/// <param name="CanReadChat">The host and people going can read the chat (site admins too, to moderate it).</param>
public sealed record WorkshopDto(
    WorkshopSummaryDto Workshop,
    string? Description,
    string? VideoLink,
    IReadOnlyList<UserSummaryDto> Attendees,
    bool CanEdit,
    bool CanJoin,
    string? CannotJoinReason,
    bool CanReadChat,
    bool CanChat,
    string? CannotChatReason);

public sealed record SaveWorkshopRequest(
    [Required, StringLength(WorkshopLimits.TitleMax)] string Title,
    [StringLength(WorkshopLimits.DescriptionMax)] string? Description,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    [StringLength(WorkshopLimits.VideoLinkMax)] string? VideoLink,
    [Range(2, WorkshopLimits.MaxPeopleLimit)] int? MaxPeople,
    Guid? ClubId = null);

/// <summary>One chat message. Ids only grow, so clients ask for the ones after the last they have.</summary>
public sealed record WorkshopMessageDto(long Id, UserSummaryDto Author, string Body, DateTimeOffset CreatedAt, bool IsHost, bool CanDelete);

public sealed record PostWorkshopMessageRequest([Required, StringLength(WorkshopLimits.MessageMax)] string Body);
