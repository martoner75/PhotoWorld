using PhotoWorld.Shared.Activities;

namespace PhotoWorld.Api.Services.Activities;

/// <summary>The pure rules behind activity, kept apart so they're easy to test.</summary>
public static class ActivityRules
{
    public const int SnippetMax = 140;

    /// <summary>Activity older than this is deleted by the background job.</summary>
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(90);

    /// <summary>Challenge news isn't sent for challenges that ended longer ago than this (e.g. when the feature launches).</summary>
    public static readonly TimeSpan ChallengeNewsWindow = TimeSpan.FromDays(3);

    /// <summary>Whether a member with <paramref name="settings"/> wants this type of activity.</summary>
    public static bool Wants(ActivitySettingsDto settings, string type) => type switch
    {
        ActivityTypes.Like => settings.Likes,
        ActivityTypes.Comment or ActivityTypes.PostComment or ActivityTypes.Feedback => settings.Comments,
        ActivityTypes.Follow => settings.Follows,
        ActivityTypes.NewPhoto or ActivityTypes.NewPost => settings.FollowedPosts,
        ActivityTypes.PhotoOfTheDay => true, // rare and always good news
        ActivityTypes.PostApproved or ActivityTypes.PostRejected
            or ActivityTypes.PhotoApproved or ActivityTypes.PhotoRejected => true, // the answer the author is waiting for
        ActivityTypes.WalkNew or ActivityTypes.WalkJoined or ActivityTypes.WalkChanged or ActivityTypes.WalkCancelled
            or ActivityTypes.WalkReminder => settings.Walks,
        ActivityTypes.LightForecast => settings.Forecasts,
        _ => settings.Challenges,
    };

    /// <summary>Likes and follows are one-per-person: liking again (after an unlike) doesn't notify twice.</summary>
    public static bool IsOncePerActor(string type) => type is ActivityTypes.Like or ActivityTypes.Follow or ActivityTypes.Feedback;

    public static string? Snippet(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= SnippetMax ? text : text[..SnippetMax].TrimEnd() + "…";
    }

    /// <summary>Which challenge news is due now. Each is sent once (the caller records it) and only while it's news.</summary>
    public static (bool Open, bool Ending, bool Results) ChallengeNewsDue(DateTimeOffset startsAt, DateTimeOffset endsAt,
        bool openSent, bool endingSent, bool resultsSent, DateTimeOffset now) =>
    (
        Open: !openSent && now >= startsAt && now < endsAt - TimeSpan.FromDays(1),
        Ending: !endingSent && now >= endsAt - TimeSpan.FromDays(1) && now < endsAt,
        Results: !resultsSent && now >= endsAt && now < endsAt + ChallengeNewsWindow
    );
}
