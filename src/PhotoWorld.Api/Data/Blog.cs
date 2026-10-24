namespace PhotoWorld.Api.Data;

/// <summary>A member's blog settings. Optional: a member's posts exist without it.</summary>
public class Blog
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Title { get; set; }
    public string? Intro { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class BlogPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public required string Title { get; set; }

    /// <summary>Sanitized HTML (see BlogHtml). Embedded photos are figures with a data-photo-id.</summary>
    public required string BodyHtml { get; set; }

    /// <summary>Plain-text preview for cards.</summary>
    public required string Excerpt { get; set; }

    /// <summary>Shown on cards: the photo the author chose (<see cref="CoverChosen"/>), otherwise the first embedded photo.</summary>
    public Guid? CoverPhotoId { get; set; }
    public Photo? CoverPhoto { get; set; }
    public bool CoverChosen { get; set; }

    public required string Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>When an admin last approved or rejected it, and (on rejection) why: shown to the author only.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public List<BlogPostPhoto> Photos { get; set; } = [];
    public List<BlogAttachment> Attachments { get; set; } = [];
    public List<BlogComment> Comments { get; set; } = [];
    public List<Report> Reports { get; set; } = [];
}

/// <summary>Which photos a post embeds; rows go when the photo is deleted, so the post stops showing it.</summary>
public class BlogPostPhoto
{
    public Guid PostId { get; set; }
    public BlogPost Post { get; set; } = null!;
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
}

/// <summary>A PDF attached to a post, stored in the attachments bucket. Rows go with the post; the API deletes the files.</summary>
public class BlogAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public BlogPost Post { get; set; } = null!;

    /// <summary>The name readers download it as (cleaned; always ends in .pdf).</summary>
    public required string FileName { get; set; }
    public long SizeBytes { get; set; }
    public required string StoragePath { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class BlogComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public BlogPost Post { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public required string Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<Report> Reports { get; set; } = [];
}
