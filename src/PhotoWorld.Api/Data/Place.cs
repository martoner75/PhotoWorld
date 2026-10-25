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

    /// <summary>The owner wants a heads-up the evening before when the golden-hour light here looks good.</summary>
    public bool ForecastAlerts { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// A light-forecast alert sent for a place and local day, so each goes out once even with several API instances
/// (the key is the claim). Goes with the place.
/// </summary>
public class PlaceForecastNotice
{
    public Guid PlaceId { get; set; }
    public Place Place { get; set; } = null!;
    public DateOnly Date { get; set; }
    public DateTimeOffset SentAt { get; set; }
}
