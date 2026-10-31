using PhotoWorld.Shared.Collabs;

namespace PhotoWorld.Api.Data;

/// <summary>
/// A collab call: a member looks for someone to shoot with (a model, a makeup artist, a second shooter…). Others see it
/// once a site admin approves it (<see cref="CollabReviewStatus"/>) and while moderation isn't hiding it. Everyone sees
/// the area; the exact meeting point goes only to the people the poster accepts. Goes with its poster's account.
/// </summary>
public class Collab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PosterId { get; set; }
    public User Poster { get; set; } = null!;

    public required string Title { get; set; }
    public required string Role { get; set; }
    public required string Terms { get; set; }
    /// <summary>The details of the terms ("€150 for the day", "15 edited photos").</summary>
    public string? TermsNote { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }

    /// <summary>What everyone sees, in the poster's words ("Naples · Centro storico").</summary>
    public required string Area { get; set; }
    /// <summary>The area's position for the board's distance sort, rounded to about 1 km like a club's.</summary>
    public double AreaLatitude { get; set; }
    public double AreaLongitude { get; set; }

    /// <summary>
    /// The exact meeting point, copied from one of the poster's places when they save the call (later changes to the
    /// place don't move it). Only the poster, the people accepted and site admins see it.
    /// </summary>
    public required string MeetingPointName { get; set; }
    public double MeetingLatitude { get; set; }
    public double MeetingLongitude { get; set; }

    public string ReviewStatus { get; set; } = CollabReviewStatus.Pending;
    public DateTimeOffset? ReviewedAt { get; set; }
    /// <summary>Why a site admin rejected it or took it down; shown to the poster only.</summary>
    public string? ReviewNote { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    /// <summary>When the people going were reminded (the day before), so it happens once.</summary>
    public DateTimeOffset? ReminderSentAt { get; set; }
    /// <summary>When everyone was asked to confirm the shoot happened (after the end), so it happens once.</summary>
    public DateTimeOffset? ConfirmRequestSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<CollabPhoto> Photos { get; set; } = [];
    public List<CollabReply> Replies { get; set; } = [];
    public List<CollabMessage> Messages { get; set; } = [];
    public List<Report> Reports { get; set; } = [];
}

/// <summary>A mood photo on a call: one of the poster's own. Goes with the call or the photo.</summary>
public class CollabPhoto
{
    public Guid CollabId { get; set; }
    public Collab Collab { get; set; } = null!;
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public int Position { get; set; }
}

/// <summary>
/// A member's reply to a call (one each). The poster accepts or declines it; once the shoot is over, each side
/// confirms it happened, and a reply both sides confirmed counts as a confirmed collab for both of them.
/// </summary>
public class CollabReply
{
    public Guid CollabId { get; set; }
    public Collab Collab { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required string Message { get; set; }
    public string Status { get; set; } = CollabReplyStatus.Pending;
    /// <summary>Show the poster the replier's public portfolio.</summary>
    public bool SharePortfolio { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    public DateTimeOffset? PosterConfirmedAt { get; set; }
    public DateTimeOffset? MemberConfirmedAt { get; set; }
}

/// <summary>A message in a call's chat (the poster and the people accepted). Ids grow, so clients poll for newer ones.</summary>
public class CollabMessage
{
    public long Id { get; set; }
    public Guid CollabId { get; set; }
    public Collab Collab { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
