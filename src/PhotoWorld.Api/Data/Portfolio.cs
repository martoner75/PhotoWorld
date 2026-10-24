namespace PhotoWorld.Api.Data;

/// <summary>
/// A member's public portfolio (one each, created when they first save it). Off until <see cref="IsPublic"/>; even
/// then it shows only the photos they chose that anyone may see (approved, not hidden), and nothing while they're
/// suspended.
/// </summary>
public class Portfolio
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>The address: /p/{slug}. Unique, lower case.</summary>
    public required string Slug { get; set; }
    public bool IsPublic { get; set; }
    public string? Bio { get; set; }
    public string? Website { get; set; }
    /// <summary>The handle only, without @.</summary>
    public string? Instagram { get; set; }
    public string? ContactEmail { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<PortfolioPhoto> Photos { get; set; } = [];
}

/// <summary>A photo in a portfolio, in the owner's order. Goes with the photo or the portfolio.</summary>
public class PortfolioPhoto
{
    public Guid UserId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public int Position { get; set; }
}
