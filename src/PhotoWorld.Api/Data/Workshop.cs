namespace PhotoWorld.Api.Data;

/// <summary>
/// An online workshop: a member hosts a session on a topic, others join and talk in its chat, optionally on a video call
/// (a link the host adds). Visible to members (not to people the host blocked or who blocked them); goes with its host's account.
/// </summary>
public class Workshop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HostId { get; set; }
    public User Host { get; set; } = null!;

    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    /// <summary>A Zoom, Meet or Teams link; only the host and people going see it.</summary>
    public string? VideoLink { get; set; }
    /// <summary>Including the host; null = no limit.</summary>
    public int? MaxPeople { get; set; }

    /// <summary>A club workshop: only the club's active members see it, hear about it and can join. Null for a public one.</summary>
    public Guid? ClubId { get; set; }
    public Club? Club { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    /// <summary>When attendees were reminded (the day before), so it happens once.</summary>
    public DateTimeOffset? ReminderSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<WorkshopAttendee> Attendees { get; set; } = [];
    public List<WorkshopMessage> Messages { get; set; } = [];
}

public class WorkshopAttendee
{
    public Guid WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; }
}

/// <summary>A message in a workshop's chat. The id is a growing number, so clients poll for the ones after the last they saw.</summary>
public class WorkshopMessage
{
    public long Id { get; set; }
    public Guid WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
