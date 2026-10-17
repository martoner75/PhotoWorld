namespace PhotoWorld.Api.Data;

/// <summary>
/// A place a member has visited, pinned on their private map. Entered by the member, never read from photos,
/// and only ever returned to its owner.
/// </summary>
public class Place
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required string Name { get; set; }
    public string? Note { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateOnly? VisitedOn { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
