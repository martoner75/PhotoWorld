using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Shared.Activities;

/// <summary>What happened. Stored as text in <c>activities.type</c>.</summary>
public static class ActivityTypes
{
    public const string Like = "like";
    public const string Comment = "comment";
    public const string PostComment = "post_comment";
    public const string Follow = "follow";
    public const string NewPhoto = "new_photo";
    public const string NewPost = "new_post";
    public const string ChallengeOpen = "challenge_open";
    public const string ChallengeEnding = "challenge_ending";
    public const string ChallengeResult = "challenge_result";

    public static readonly IReadOnlyList<string> All =
        [Like, Comment, PostComment, Follow, NewPhoto, NewPost, ChallengeOpen, ChallengeEnding, ChallengeResult];
}

/// <summary>
/// One notification. <see cref="Place"/> is set on challenge results (1 = won); the photo, post and challenge fields
/// are filled in when the activity is about one of them.
/// </summary>
public sealed record ActivityDto(
    Guid Id,
    string Type,
    UserSummaryDto? Actor,
    Guid? PhotoId,
    string? PhotoTitle,
    string? PhotoThumbnailUrl,
    Guid? PostId,
    string? PostTitle,
    Guid? ChallengeId,
    string? ChallengeTitle,
    int? Place,
    string? Snippet,
    DateTimeOffset CreatedAt,
    bool IsRead);

/// <summary>Which kinds of activity a member wants. Everything is on until they change it.</summary>
public sealed record ActivitySettingsDto(bool Likes, bool Comments, bool Follows, bool FollowedPosts, bool Challenges)
{
    public static readonly ActivitySettingsDto AllOn = new(true, true, true, true, true);
}

public sealed record UnreadActivityDto(int Count);
