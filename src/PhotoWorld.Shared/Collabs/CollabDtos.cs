using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Shared.Collabs;

public static class CollabLimits
{
    public const int TitleMax = 100;
    public const int DescriptionMax = 2000;
    public const int TermsNoteMax = 200;
    public const int AreaMax = 80;
    public const int ReplyMax = 1000;
    public const int MessageMax = 1000;
    /// <summary>Mood photos: the poster's own, to show the idea.</summary>
    public const int MaxPhotos = 6;
    /// <summary>People a poster can accept on one call.</summary>
    public const int MaxAccepted = 10;
    /// <summary>Calls a member can have open (not ended, not cancelled) at once, so the board isn't flooded.</summary>
    public const int MaxOpenCalls = 5;
    public static readonly TimeSpan MaxLength = TimeSpan.FromHours(12);

    /// <summary>The board lists calls within this distance unless the member asks for another.</summary>
    public const double DefaultNearKm = 50;
    public const double MaxNearKm = 150;

    /// <summary>The chat stays open this long after the shoot, for sending photos and saying thanks; then it's read-only.</summary>
    public static readonly TimeSpan ChatOpenAfter = TimeSpan.FromDays(7);
    /// <summary>How long after the shoot people can confirm it happened.</summary>
    public static readonly TimeSpan ConfirmWithin = TimeSpan.FromDays(30);

    public const int MessagesShown = 100;
    public static readonly TimeSpan ChatRefresh = TimeSpan.FromSeconds(5);

    /// <summary>The tag members put on photos from a collab: "collab-" and the start of its id.</summary>
    public static string TagFor(Guid collabId) => "collab-" + collabId.ToString("N")[..8];
}

/// <summary>Who a call is looking for. Stored as text in <c>collabs.role</c>.</summary>
public static class CollabRoles
{
    public const string Model = "model";
    public const string Makeup = "makeup";
    public const string SecondShooter = "second_shooter";
    public const string Assistant = "assistant";
    public const string Photographer = "photographer";

    public static readonly IReadOnlyList<string> All = [Model, Makeup, SecondShooter, Assistant, Photographer];

    public static string Label(string role) => role switch
    {
        Model => "Model",
        Makeup => "Makeup & hair",
        SecondShooter => "Second shooter",
        Assistant => "Assistant",
        Photographer => "Photographer",
        _ => role,
    };

    /// <summary>For filters: "Models", "Second shooters", …</summary>
    public static string PluralLabel(string role) => role switch
    {
        Model => "Models",
        Makeup => "Makeup & hair",
        SecondShooter => "Second shooters",
        Assistant => "Assistants",
        Photographer => "Photographers",
        _ => role,
    };
}

/// <summary>What people get for taking part. Every call states one, so nobody finds out on the day.</summary>
public static class CollabTerms
{
    public const string Paid = "paid";
    /// <summary>Trade for photos (TFP): everyone gives time, everyone gets pictures.</summary>
    public const string Trade = "trade";
    public const string Unpaid = "unpaid";

    public static readonly IReadOnlyList<string> All = [Paid, Trade, Unpaid];

    public static string Label(string terms) => terms switch
    {
        Paid => "Paid",
        Trade => "Trade for photos",
        Unpaid => "Unpaid",
        _ => terms,
    };
}

/// <summary>A call is approved by a site admin before others see it, like a club.</summary>
public static class CollabReviewStatus
{
    public const string Pending = PhotoReviewStatus.Pending;
    public const string Approved = PhotoReviewStatus.Approved;
    public const string Rejected = PhotoReviewStatus.Rejected;

    public static readonly IReadOnlyList<string> All = [Pending, Approved, Rejected];
}

public static class CollabReplyStatus
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Declined = "declined";

    public static readonly IReadOnlyList<string> All = [Pending, Accepted, Declined];
}

/// <summary>Which calls to list.</summary>
public static class CollabLists
{
    /// <summary>Approved calls that haven't started yet (the board).</summary>
    public const string Open = "open";
    /// <summary>Calls the caller posted or replied to, any state, newest first.</summary>
    public const string Mine = "mine";
}

/// <param name="Area">What everyone sees of the place, in the poster's words ("Naples · Centro storico").</param>
/// <param name="PosterConfirmedCount">Collabs the poster has done that both sides confirmed.</param>
/// <param name="CoverUrl">The first mood photo's thumbnail, if any.</param>
/// <param name="DistanceKm">From the place or location the caller searched near; null without one.</param>
/// <param name="MyReplyStatus">The caller's reply, if they replied (<see cref="CollabReplyStatus"/>).</param>
public sealed record CollabSummaryDto(
    Guid Id,
    string Title,
    string Role,
    string Terms,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Area,
    UserSummaryDto Poster,
    int PosterConfirmedCount,
    int ReplyCount,
    string? CoverUrl,
    double? DistanceKm,
    bool IsPoster,
    string? MyReplyStatus,
    bool IsCancelled,
    string ReviewStatus);

/// <summary>The exact meeting point: only for the poster, the people they accepted, and site admins.</summary>
public sealed record CollabMeetingPointDto(string Name, double Latitude, double Longitude);

/// <summary>A reply to a call. The poster sees all of them; a member sees only their own.</summary>
/// <param name="ConfirmedCount">Confirmed collabs the replier has done, so the poster can see who they're dealing with.</param>
/// <param name="PortfolioSlug">Their public portfolio, if they chose to share it and it's public.</param>
public sealed record CollabReplyDto(
    UserSummaryDto Member,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    int ConfirmedCount,
    string? PortfolioSlug,
    bool PosterConfirmed,
    bool MemberConfirmed);

/// <summary>
/// A call's page. What's filled in depends on who's looking: the meeting point and chat are for the poster and the people
/// they accepted; the replies list is for the poster.
/// </summary>
/// <param name="ReviewNote">Why an admin turned it down; for the poster only.</param>
/// <param name="Replies">Every reply, for the poster (and site admins); empty for everyone else.</param>
/// <param name="MyReply">The caller's own reply, if any.</param>
/// <param name="Accepted">The people the poster accepted, for the poster and the people accepted.</param>
/// <param name="AreaLatitude">The area's position, rounded to about 1 km like a club's: for the light on the day, not directions.</param>
/// <param name="CanEdit">The poster (edit, cancel, delete) or a site admin (cancel, delete); only the poster edits.</param>
/// <param name="CanConfirm">The caller can now say the shoot happened (with the poster, or for the poster with someone they accepted).</param>
public sealed record CollabDto(
    CollabSummaryDto Collab,
    string? Description,
    string? TermsNote,
    string Tag,
    double AreaLatitude,
    double AreaLongitude,
    IReadOnlyList<PhotoSummaryDto> Photos,
    CollabMeetingPointDto? MeetingPoint,
    IReadOnlyList<CollabReplyDto> Replies,
    CollabReplyDto? MyReply,
    IReadOnlyList<UserSummaryDto> Accepted,
    string? ReviewNote,
    bool CanEdit,
    bool CanReply,
    string? CannotReplyReason,
    bool CanReadChat,
    bool CanChat,
    string? CannotChatReason,
    bool CanConfirm,
    bool CanReview);

/// <param name="PlaceId">One of the poster's places: its name and position become the meeting point.</param>
/// <param name="PhotoIds">Up to <see cref="CollabLimits.MaxPhotos"/> of the poster's own photos, in order.</param>
public sealed record SaveCollabRequest(
    [Required, StringLength(CollabLimits.TitleMax)] string Title,
    [Required] string Role,
    [Required] string Terms,
    [StringLength(CollabLimits.TermsNoteMax)] string? TermsNote,
    [StringLength(CollabLimits.DescriptionMax)] string? Description,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    [Required, StringLength(CollabLimits.AreaMax)] string Area,
    Guid PlaceId,
    IReadOnlyList<Guid>? PhotoIds);

public sealed record ReplyToCollabRequest(
    [Required, StringLength(CollabLimits.ReplyMax)] string Message,
    bool SharePortfolio = true);

public sealed record RejectCollabRequest([StringLength(500)] string? Reason);

public sealed record CollabMessageDto(long Id, UserSummaryDto Author, string Body, DateTimeOffset CreatedAt, bool IsPoster, bool CanDelete);

public sealed record PostCollabMessageRequest([Required, StringLength(CollabLimits.MessageMax)] string Body);
