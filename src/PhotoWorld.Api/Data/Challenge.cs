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
