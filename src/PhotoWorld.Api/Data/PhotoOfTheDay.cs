namespace PhotoWorld.Api.Data;

/// <summary>An admin's pick for one day (UTC). Goes if the photo goes; the owner hears about it on the day.</summary>
public class PhotoOfTheDay
{
    public DateOnly Date { get; set; }
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public Guid? ChosenById { get; set; }
    public User? ChosenBy { get; set; }

    /// <summary>Why it was picked, shown with it (optional).</summary>
    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the owner was told (by ScheduledActivitySender, on the day).</summary>
    public DateTimeOffset? NotifiedAt { get; set; }
}
