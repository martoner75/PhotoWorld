using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Shared.Clubs;

public static class ClubLimits
{
    public const int NameMax = 80;
    public const int DescriptionMax = 1000;
    public const int CityMax = 80;
    public const int ReviewNoteMax = 500;

    /// <summary>Clubs one member can own.</summary>
    public const int MaxOwned = 3;

    /// <summary>Clubs one member can belong to (owning and pending requests count).</summary>
    public const int MaxJoined = 25;

    /// <summary>Members in one club, pending requests included.</summary>
    public const int MaxMembers = 500;

    /// <summary>How far "near" reaches when sorting clubs by distance.</summary>
    public const double NearKm = 150;
}

/// <summary>How members get in: anyone can join, or the club's admins approve each request.</summary>
public static class ClubJoinPolicy
{
    public const string Open = "open";
    public const string Approval = "approval";

    public static readonly IReadOnlyList<string> All = [Open, Approval];
}

/// <summary>
/// The owner (one, who created it or was handed it) and admins run the club: edit it, approve requests, remove
/// members. Only the owner can make or remove admins, hand the club over, or delete it.
/// </summary>
public static class ClubRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";

    public static readonly IReadOnlyList<string> All = [Owner, Admin, Member];
}

public static class ClubMemberStatus
{
    public const string Active = "active";

    /// <summary>Asked to join a club that approves requests; not a member yet.</summary>
    public const string Pending = "pending";

    public static readonly IReadOnlyList<string> All = [Active, Pending];
}

/// <summary>
/// A new club waits (pending) until a site admin approves it; until then only its owner and site admins see it.
/// A rejected club stays visible to its owner only, with the admin's note. Clubs created by site admins are approved
/// straight away. Uses the same values as <see cref="PhotoReviewStatus"/>.
/// </summary>
public static class ClubReviewStatus
{
    public const string Pending = PhotoReviewStatus.Pending;
    public const string Approved = PhotoReviewStatus.Approved;
    public const string Rejected = PhotoReviewStatus.Rejected;

    public static readonly IReadOnlyList<string> All = [Pending, Approved, Rejected];
}

/// <summary>A club in the directory or a member's list.</summary>
/// <param name="MyRole">The viewer's role, or null when they aren't in it. <paramref name="MyStatus"/> says if it's a pending request.</param>
/// <param name="DistanceKm">From the place the directory was sorted by, when it was.</param>
public sealed record ClubSummaryDto(
    Guid Id,
    string Name,
    string? City,
    string JoinPolicy,
    int MemberCount,
    string ReviewStatus,
    string? MyRole,
    string? MyStatus,
    double? DistanceKm = null);

/// <summary>One club's page. The Can… flags say what the viewer may do.</summary>
public sealed record ClubDto(
    ClubSummaryDto Summary,
    string? Description,
    double? Latitude,
    double? Longitude,
    UserSummaryDto Owner,
    DateTimeOffset CreatedAt,
    bool CanEdit,
    bool CanManageMembers,
    bool IsOwner,
    bool CanJoin,
    bool CanLeave,
    bool CanReport,
    bool CanReview,
    int PendingRequests,
    ModerationState Moderation,
    string? ReviewNote);

/// <param name="IsMe">The viewer themself, so screens don't offer to manage them.</param>
public sealed record ClubMemberDto(UserSummaryDto User, string Role, string Status, DateTimeOffset JoinedAt, bool IsMe = false);

public sealed record SaveClubRequest(
    [Required, StringLength(ClubLimits.NameMax, MinimumLength = 1)] string Name,
    [StringLength(ClubLimits.DescriptionMax)] string? Description,
    [StringLength(ClubLimits.CityMax)] string? City,
    [Required] string JoinPolicy,
    [Range(-90.0, 90.0)] double? Latitude,
    [Range(-180.0, 180.0)] double? Longitude);

/// <summary>The owner makes a member an admin or a member again, or hands the club over ("owner").</summary>
public sealed record SetClubRoleRequest([Required] string Role);

public sealed record RejectClubRequest([StringLength(ClubLimits.ReviewNoteMax)] string? Reason);
