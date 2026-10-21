namespace PhotoWorld.Api.Data;

/// <summary>A member following another, so the followee's photos and posts show up in their "Following" tabs.</summary>
public class Follow
{
    public Guid FollowerId { get; set; }
    public User Follower { get; set; } = null!;
    public Guid FolloweeId { get; set; }
    public User Followee { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
