namespace PhotoWorld.Api.Data;

/// <summary>
/// A themed photo challenge, open for entries from <see cref="StartsAt"/> until <see cref="EndsAt"/>. There's no
/// admin page: challenges are added in the Supabase dashboard (see README → Photo challenges).
/// </summary>
public class Challenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // When the activity about this challenge went out (see ScheduledActivitySender), so each goes out once.
    public DateTimeOffset? OpenNotifiedAt { get; set; }
    public DateTimeOffset? EndingNotifiedAt { get; set; }
    public DateTimeOffset? ResultsNotifiedAt { get; set; }

    /// <summary>
    /// A club challenge, run by the club's owner and admins: only its active members can enter, and only they hear
    /// about it. Null for a site-wide challenge, run by site admins.
    /// </summary>
    public Guid? ClubId { get; set; }
    public Club? Club { get; set; }

    public List<ChallengeEntry> Entries { get; set; } = [];
}

/// <summary>A member's own photo entered in a challenge. Goes when the photo (or its owner's account) goes.</summary>
public class ChallengeEntry
{
    public Guid ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;

    /// <summary>Votes are likes from this moment until the challenge ends.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
