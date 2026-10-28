using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Services.Clubs;

/// <summary>A problem with a club request, for one form field (null = the whole form).</summary>
public sealed record ClubProblem(string? Field, string Message);

/// <summary>Who may do what in a club, kept apart from the controller so it's easy to test.</summary>
public static class ClubRules
{
    /// <summary>The owner and admins run the club day to day.</summary>
    public static bool CanManage(string? role) => role is ClubRoles.Owner or ClubRoles.Admin;

    public static IReadOnlyList<ClubProblem> CannotSave(SaveClubRequest request)
    {
        var problems = new List<ClubProblem>();
        if (string.IsNullOrWhiteSpace(request.Name)) problems.Add(new("name", "Give the club a name."));
        if (!ClubJoinPolicy.All.Contains(request.JoinPolicy)) problems.Add(new("joinPolicy", "Choose how people join."));
        if ((request.Latitude is null) != (request.Longitude is null)
            || request.Latitude is { } lat && !double.IsFinite(lat) || request.Longitude is { } lon && !double.IsFinite(lon))
            problems.Add(new("latitude", "Pick the club's area again."));
        return problems;
    }

    /// <summary>Why the member can't join (or ask to), or null if they can.</summary>
    public static string? CannotJoin(bool alreadyIn, int clubsJoined, int clubMembers, bool blockedByOwner)
    {
        if (alreadyIn) return "You're already in this club, or waiting to be let in.";
        if (blockedByOwner) return "You can't join this club.";
        if (clubsJoined >= ClubLimits.MaxJoined) return $"You can be in up to {ClubLimits.MaxJoined} clubs. Leave one first.";
        if (clubMembers >= ClubLimits.MaxMembers) return "This club is full.";
        return null;
    }

    /// <summary>Joining an open club makes you a member; otherwise it's a request the club's admins approve.</summary>
    public static string StatusOnJoin(string joinPolicy) =>
        joinPolicy == ClubJoinPolicy.Open ? ClubMemberStatus.Active : ClubMemberStatus.Pending;

    /// <summary>The owner has to hand the club over (or delete it) before leaving.</summary>
    public static string? CannotLeave(string role) =>
        role == ClubRoles.Owner ? "Hand the club to another member first, or delete it." : null;

    /// <summary>
    /// Admins can remove members (and turn down requests); only the owner can remove an admin. Nobody removes the
    /// owner, and leaving yourself is <see cref="CannotLeave"/>.
    /// </summary>
    public static string? CannotRemove(string actorRole, string targetRole, bool isSelf)
    {
        if (isSelf) return "Use Leave to leave the club.";
        if (!CanManage(actorRole)) return "Only the club's admins can remove members.";
        if (targetRole == ClubRoles.Owner) return "The owner can't be removed.";
        if (targetRole == ClubRoles.Admin && actorRole != ClubRoles.Owner) return "Only the owner can remove an admin.";
        return null;
    }

    /// <summary>
    /// Only the owner changes roles: making an active member an admin or a member again, or handing over the club
    /// (the new owner must be an active member; the old owner becomes an admin).
    /// </summary>
    public static string? CannotSetRole(string actorRole, string targetStatus, string newRole, bool isSelf)
    {
        if (actorRole != ClubRoles.Owner) return "Only the club's owner can change roles.";
        if (!ClubRoles.All.Contains(newRole)) return "That isn't a club role.";
        if (isSelf) return "Hand the club to another member to change your own role.";
        return targetStatus != ClubMemberStatus.Active ? "Approve their request first." : null;
    }

    /// <summary>
    /// Why a photo can't be shared to a club, or null if it can: members share their own photos (not refused ones) to
    /// approved clubs they're active in.
    /// </summary>
    public static string? CannotShare(bool isActiveMember, bool clubApproved, bool ownsPhoto, string photoReviewStatus)
    {
        if (!ownsPhoto) return "You can only share your own photos.";
        if (!isActiveMember) return "Join the club to share photos there.";
        if (!clubApproved) return "The club has to be approved before photos can be shared to it.";
        if (photoReviewStatus == PhotoReviewStatus.Rejected) return "That photo wasn't approved, so it can't be shared.";
        return null;
    }

    /// <summary>Any active member can organise a walk for an approved club.</summary>
    public static string? CannotOrganizeWalk(bool isActiveMember, bool clubApproved) =>
        !isActiveMember ? "Only the club's members can organise a walk for it."
        : !clubApproved ? "The club has to be approved first."
        : null;

    /// <summary>Any active member can host a workshop for an approved club.</summary>
    public static string? CannotHostWorkshop(bool isActiveMember, bool clubApproved) =>
        !isActiveMember ? "Only the club's members can host a workshop for it."
        : !clubApproved ? "The club has to be approved first."
        : null;

    /// <summary>The club's owner and admins run its challenges (create, change, delete) once the club is approved.</summary>
    public static string? CannotRunChallenge(string? role, bool clubApproved) =>
        !CanManage(role) ? "Only the club's owner and admins can run its challenges."
        : !clubApproved ? "The club has to be approved first."
        : null;

    /// <summary>A club challenge is for the club's active members only.</summary>
    public static string? CannotEnterClubChallenge(bool isActiveMember) =>
        isActiveMember ? null : "This challenge is for the club's members. Join the club to enter.";

    /// <summary>The owner and admins pick the cover, from photos in the club's feed (or none, for the newest).</summary>
    public static string? CannotSetCover(string? role, bool photoInFeed) =>
        !CanManage(role) ? "Only the club's owner and admins choose its cover."
        : !photoInFeed ? "Choose one of the photos shared to the club that everyone can see."
        : null;

    /// <summary>A shared photo comes out of the club's feed at its owner's wish, or a club admin's.</summary>
    public static bool CanUnshare(bool ownsPhoto, string? role) => ownsPhoto || CanManage(role);

    /// <summary>Great-circle distance in kilometres.</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 6371 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>A club's area: two decimals, about 1 km. Enough to sort by distance without pinning an address.</summary>
    public static double? Area(double? degrees) => degrees is { } d ? Math.Round(d, 2) : null;
}
