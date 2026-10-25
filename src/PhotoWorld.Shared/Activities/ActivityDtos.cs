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
    public const string PhotoOfTheDay = "photo_of_the_day";
    public const string Feedback = "feedback";
    public const string WalkNew = "walk_new";
    public const string WalkJoined = "walk_joined";
    public const string WalkChanged = "walk_changed";
    public const string WalkCancelled = "walk_cancelled";
    public const string WalkReminder = "walk_reminder";
    /// <summary>An admin approved the member's post (now published) or sent it back (the snippet is their reason).</summary>
    public const string PostApproved = "post_approved";
    public const string PostRejected = "post_rejected";
    /// <summary>The same for an uploaded photo.</summary>
    public const string PhotoApproved = "photo_approved";
    public const string PhotoRejected = "photo_rejected";
    /// <summary>The golden-hour light at one of the member's places looks good tomorrow (the snippet says where and why).</summary>
    public const string LightForecast = "light_forecast";

    public static readonly IReadOnlyList<string> All =
        [Like, Comment, PostComment, Follow, NewPhoto, NewPost, ChallengeOpen, ChallengeEnding, ChallengeResult, PhotoOfTheDay, Feedback,
         WalkNew, WalkJoined, WalkChanged, WalkCancelled, WalkReminder, PostApproved, PostRejected, PhotoApproved, PhotoRejected, LightForecast];
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
    bool IsRead,
    Guid? WalkId = null,
    string? WalkTitle = null);

/// <summary>Which kinds of activity a member wants. Everything is on until they change it.</summary>
/// <param name="Push">Also send them to the member's phones (where the app is allowed to show notifications).</param>
public sealed record ActivitySettingsDto(bool Likes, bool Comments, bool Follows, bool FollowedPosts, bool Challenges, bool Push = true,
    bool Walks = true, bool Forecasts = true)
{
    public static readonly ActivitySettingsDto AllOn = new(true, true, true, true, true, true, true, true);
}

public static class DevicePlatforms
{
    public const string Ios = "ios";
    public const string Android = "android";
}

/// <summary>A phone asking for push notifications: its APNs device token (iOS) or FCM registration token (Android).</summary>
public sealed record RegisterDeviceRequest(
    [System.ComponentModel.DataAnnotations.Required] string Platform,
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(512, MinimumLength = 8)] string Token);

public sealed record UnreadActivityDto(int Count);
