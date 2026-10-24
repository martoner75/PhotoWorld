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

    /// <summary>
    /// Optional unedited version, shown with a before/after slider. Processed like the photo (metadata stripped, same
    /// watermark); adding one sends a member's approved photo back for approval, since it's a new image.
    /// </summary>
    public string? BeforeDisplayPath { get; set; }
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

    /// <summary>The owner asks other members for feedback (composition, light, focus, comment).</summary>
    public bool WantsFeedback { get; set; }

    public List<PhotoFeedback> Feedback { get; set; } = [];

    /// <summary>
    /// Off: the site and apps hide "open full size" and saving, and discourage right-click and dragging. A deterrent,
    /// not a lock: the image is still sent to the browser to be shown.
    /// </summary>
    public bool AllowDownload { get; set; } = true;

    /// <summary>The owner's name was drawn on the image at upload (it can't be removed later; originals aren't kept).</summary>
    public bool Watermarked { get; set; }

    /// <summary>The owner opted in to the public home-page slideshow.</summary>
    public bool ShowOnHome { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>See <see cref="PhotoWorld.Shared.Photos.PhotoReviewStatus"/>; only approved photos are shown to others.</summary>
    public string ReviewStatus { get; set; } = PhotoWorld.Shared.Photos.PhotoReviewStatus.Approved;
    public DateTimeOffset? ReviewedAt { get; set; }
    /// <summary>Why an admin rejected it; shown to the owner only.</summary>
    public string? ReviewNote { get; set; }

    public List<PhotoTag> Tags { get; set; } = [];
    public List<PhotoLike> Likes { get; set; } = [];
    public List<PhotoComment> Comments { get; set; } = [];
    public List<Report> Reports { get; set; } = [];
}

/// <summary>One member's feedback on a photo (one each; they can change it). Private between them and the owner.</summary>
public class PhotoFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public int Composition { get; set; }
    public int Light { get; set; }
    public int Focus { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>How many times other members opened a photo's page on a day (UTC). Counts only: viewers aren't recorded.</summary>
public class PhotoViewDay
{
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public DateOnly Day { get; set; }
    public int Views { get; set; }
}

/// <summary>A tag on a photo, stored normalised without '#' (see PhotoTags).</summary>
public class PhotoTag
{
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public required string Name { get; set; }
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
