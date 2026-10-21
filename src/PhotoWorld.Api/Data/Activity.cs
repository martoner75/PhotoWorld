namespace PhotoWorld.Api.Data;

/// <summary>
/// A notification for <see cref="RecipientId"/>: something <see cref="ActorId"/> did (null for challenge news), about a
/// photo, post or challenge. Rows go when any of those go. Types are in <c>ActivityTypes</c>.
/// </summary>
public class Activity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientId { get; set; }
    public User Recipient { get; set; } = null!;
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }

    public required string Type { get; set; }
    public Guid? PhotoId { get; set; }
    public Photo? Photo { get; set; }
    public Guid? PostId { get; set; }
    public BlogPost? Post { get; set; }
    public Guid? ChallengeId { get; set; }
    public Challenge? Challenge { get; set; }
    public Guid? WalkId { get; set; }
    public Walk? Walk { get; set; }

    /// <summary>Challenge results: 1 = won, 2 or 3 = placed; null = took part.</summary>
    public int? Place { get; set; }

    /// <summary>The start of a comment, so the list reads well without opening it.</summary>
    public string? Snippet { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>When PushDispatcher dealt with it (sent to the recipient's phones, or found there was nothing to send).</summary>
    public DateTimeOffset? PushedAt { get; set; }
}

/// <summary>A member's choice of activity (one row, created when they first change it; no row = everything on).</summary>
public class ActivitySettings
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public bool Likes { get; set; } = true;
    public bool Comments { get; set; } = true;
    public bool Follows { get; set; } = true;
    public bool FollowedPosts { get; set; } = true;
    public bool Challenges { get; set; } = true;
    public bool Push { get; set; } = true;
    public bool Walks { get; set; } = true;
}

/// <summary>A member's phone that receives push notifications. Goes with the account; dead tokens are removed when sending.</summary>
public class DeviceRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Platform { get; set; }
    public required string Token { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
