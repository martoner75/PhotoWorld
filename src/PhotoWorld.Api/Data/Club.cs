using PhotoWorld.Shared.Clubs;

namespace PhotoWorld.Api.Data;

/// <summary>
/// A camera club: members, run by its owner and admins. Visible to others once a site admin approves it (see
/// <see cref="ClubReviewStatus"/>) and while moderation isn't hiding it. Goes when its owner's account goes.
/// </summary>
public class Club
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>Where the club meets, as members would say it ("Bristol").</summary>
    public string? City { get; set; }

    /// <summary>Optional, for sorting the directory by distance. Rounded to about 1 km: an area, not an address.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string JoinPolicy { get; set; } = ClubJoinPolicy.Open;

    public string ReviewStatus { get; set; } = ClubReviewStatus.Pending;
    public DateTimeOffset? ReviewedAt { get; set; }
    /// <summary>Why a site admin rejected it or took it down; shown to the owner only.</summary>
    public string? ReviewNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<ClubMember> Members { get; set; } = [];
    public List<Report> Reports { get; set; } = [];
}

/// <summary>A member of a club, or someone asking to join (<see cref="ClubMemberStatus.Pending"/>). The owner has a row too.</summary>
public class ClubMember
{
    public Guid ClubId { get; set; }
    public Club Club { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Role { get; set; } = ClubRoles.Member;
    public string Status { get; set; } = ClubMemberStatus.Active;

    /// <summary>When they joined, or asked to.</summary>
    public DateTimeOffset JoinedAt { get; set; }
}

/// <summary>
/// A member's photo shared to one of their clubs, for the club's feed. The photo stays the member's (and as visible
/// as any photo); the share goes when they leave the club, or when the photo or club goes.
/// </summary>
public class ClubPhoto
{
    public Guid ClubId { get; set; }
    public Club Club { get; set; } = null!;
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public DateTimeOffset SharedAt { get; set; }
}
