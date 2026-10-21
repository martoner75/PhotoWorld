namespace PhotoWorld.Api.Data;

/// <summary>
/// A member blocking another: the blocker stops seeing the blocked member's photos, posts and comments, and the
/// blocked member can't follow the blocker or comment on their photos and posts (see <see cref="Moderation"/>).
/// </summary>
public class Block
{
    public Guid BlockerId { get; set; }
    public User Blocker { get; set; } = null!;
    public Guid BlockedId { get; set; }
    public User Blocked { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
