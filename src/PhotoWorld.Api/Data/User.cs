namespace PhotoWorld.Api.Data;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DisplayName { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastLoginAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Can use the admin pages (column <c>is_admin</c>). Set on the Users page or in the database; never in config.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Set when an admin suspends the account: the member can't sign in, their current session ends, and everything
    /// they shared is hidden (see <see cref="Moderation"/>). Nothing is deleted; reactivating brings it all back.
    /// </summary>
    public DateTimeOffset? SuspendedAt { get; set; }
    public string? SuspensionReason { get; set; }

    public List<ExternalLogin> ExternalLogins { get; set; } = [];

    /// <summary>Blocks against this member, so visibility rules can ask "has the viewer blocked the author?".</summary>
    public List<Block> BlockedBy { get; set; } = [];
}

/// <summary>Links a PhotoWorld user to an external identity provider account (e.g. Google).</summary>
public class ExternalLogin
{
    public int Id { get; set; }
    public required string Provider { get; set; }
    public required string ProviderUserId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
