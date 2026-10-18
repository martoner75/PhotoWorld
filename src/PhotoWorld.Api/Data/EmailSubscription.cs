namespace PhotoWorld.Api.Data;

/// <summary>
/// A member's email notifications about new blog posts (one row per member, created when they first save).
/// Emails go out only when <see cref="Enabled"/> and the address is confirmed.
/// </summary>
public class EmailSubscription
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required string Email { get; set; }
    public required string Frequency { get; set; }
    public bool Enabled { get; set; }

    /// <summary>Set when the member clicks the confirmation link, or straight away for their verified account email.</summary>
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>SHA-256 of the pending confirmation link's token; the token itself only ever exists in the email.</summary>
    public string? ConfirmTokenHash { get; set; }
    public DateTimeOffset? ConfirmTokenExpiresAt { get; set; }

    /// <summary>
    /// Secret in every email's unsubscribe link. Stored as is (it must go into each email) because all it can do is
    /// switch notifications off.
    /// </summary>
    public required string UnsubscribeToken { get; set; }

    /// <summary>Posts published up to this moment have been dealt with; only later ones are news.</summary>
    public DateTimeOffset NotifiedThrough { get; set; }

    /// <summary>Start of the current daily/weekly digest period.</summary>
    public DateTimeOffset PeriodStartedAt { get; set; }

    public DateTimeOffset? LastSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
