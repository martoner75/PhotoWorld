namespace PhotoWorld.Api.Data;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DisplayName { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastLoginAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ExternalLogin> ExternalLogins { get; set; } = [];
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
