namespace PhotoWorld.Api.Data;

/// <summary>
/// A member's report about a photo, photo comment, blog post, blog comment, club or collab call (exactly one is set). Moderators review reports in Supabase
/// by changing <see cref="Status"/>; see README → Reports.
/// </summary>
public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = null!;

    public Guid? PhotoId { get; set; }
    public Photo? Photo { get; set; }
    public Guid? CommentId { get; set; }
    public PhotoComment? Comment { get; set; }
    public Guid? BlogPostId { get; set; }
    public BlogPost? BlogPost { get; set; }
    public Guid? BlogCommentId { get; set; }
    public BlogComment? BlogComment { get; set; }
    public Guid? ClubId { get; set; }
    public Club? Club { get; set; }
    public Guid? CollabId { get; set; }
    public Collab? Collab { get; set; }

    public required string Reason { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = ReportStatus.Open;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

public static class ReportStatus
{
    public const string Open = "open";
    public const string Dismissed = "dismissed";
    public const string Upheld = "upheld";
}
