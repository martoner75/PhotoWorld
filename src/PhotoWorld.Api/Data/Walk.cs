namespace PhotoWorld.Api.Data;

/// <summary>
/// A photo walk: members meet at a point and shoot together. Visible to members (not to people the organiser
/// blocked or who blocked them); goes with its organiser's account.
/// </summary>
public class Walk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizerId { get; set; }
    public User Organizer { get; set; } = null!;

    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public required string MeetingPoint { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    /// <summary>Including the organiser; null = no limit.</summary>
    public int? MaxPeople { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    /// <summary>When attendees were reminded (the day before), so it happens once.</summary>
    public DateTimeOffset? ReminderSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<WalkAttendee> Attendees { get; set; } = [];
}

public class WalkAttendee
{
    public Guid WalkId { get; set; }
    public Walk Walk { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; }
}
