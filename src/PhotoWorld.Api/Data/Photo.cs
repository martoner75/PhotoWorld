namespace PhotoWorld.Api.Data;

public class Photo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Object paths inside the Supabase photos bucket. Originals are never stored.</summary>
    public required string DisplayPath { get; set; }
    public required string ThumbnailPath { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long SizeBytes { get; set; }

    // Camera settings read from EXIF at upload time (GPS is never read).
    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }
    public string? Lens { get; set; }
    public decimal? FocalLengthMm { get; set; }
    public decimal? Aperture { get; set; }
    public string? ExposureTime { get; set; }
    public int? Iso { get; set; }
    public DateTimeOffset? TakenAt { get; set; }

    /// <summary>
    /// One of the owner's places, chosen to show where the photo was taken (Spots map). Cleared if the place is
    /// deleted. Only the place's name and position are shown with the photo.
    /// </summary>
    public Guid? PlaceId { get; set; }
    public Place? Place { get; set; }

    /// <summary>The owner opted in to the public home-page slideshow.</summary>
    public bool ShowOnHome { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<PhotoLike> Likes { get; set; } = [];
    public List<PhotoComment> Comments { get; set; } = [];
    public List<Report> Reports { get; set; } = [];
}

public class PhotoLike
{
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class PhotoComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public required string Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<Report> Reports { get; set; } = [];
}
