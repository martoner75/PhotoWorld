namespace PhotoWorld.Shared.Activities;

/// <summary>Activity wording shared by the website and the app.</summary>
public static class ActivityText
{
    /// <summary>"Anna liked your photo “Fog”", "You won “Fog”!", …</summary>
    public static string Describe(ActivityDto a)
    {
        var who = a.Actor?.DisplayName ?? "Someone";
        var photo = Quote(a.PhotoTitle);
        var post = Quote(a.PostTitle);
        var challenge = Quote(a.ChallengeTitle);
        return a.Type switch
        {
            ActivityTypes.Like => $"{who} liked your photo {photo}",
            ActivityTypes.Comment => $"{who} commented on your photo {photo}",
            ActivityTypes.PostComment => $"{who} commented on your post {post}",
            ActivityTypes.Follow => $"{who} started following you",
            ActivityTypes.NewPhoto => $"{who} shared a new photo, {photo}",
            ActivityTypes.NewPost => $"{who} published a new post, {post}",
            ActivityTypes.ChallengeOpen => $"New photo challenge: {challenge} is open for entries",
            ActivityTypes.ChallengeEnding => $"The {challenge} challenge ends in less than a day",
            ActivityTypes.ChallengeResult => a.Place switch
            {
                1 => $"You won the {challenge} challenge! 🏆",
                { } place => $"You placed #{place} in the {challenge} challenge",
                _ => $"The results of the {challenge} challenge are in",
            },
            _ => "Something happened on PhotoWorld",
        };
    }

    /// <summary>The settings switch an activity belongs to, as a label.</summary>
    public static readonly IReadOnlyDictionary<string, string> SettingLabels = new Dictionary<string, string>
    {
        [nameof(ActivitySettingsDto.Likes)] = "Likes on your photos",
        [nameof(ActivitySettingsDto.Comments)] = "Comments on your photos and posts",
        [nameof(ActivitySettingsDto.Follows)] = "New followers",
        [nameof(ActivitySettingsDto.FollowedPosts)] = "New photos and posts from members you follow",
        [nameof(ActivitySettingsDto.Challenges)] = "Photo challenges: new ones, closing soon, results",
    };

    /// <summary>"just now", "5 min ago", "3 h ago", "2 days ago", then the date.</summary>
    public static string Ago(DateTimeOffset when, DateTimeOffset now)
    {
        var age = now - when;
        return age.TotalMinutes < 1 ? "just now"
            : age.TotalHours < 1 ? $"{(int)age.TotalMinutes} min ago"
            : age.TotalDays < 1 ? $"{(int)age.TotalHours} h ago"
            : age.TotalDays < 7 ? ((int)age.TotalDays == 1 ? "yesterday" : $"{(int)age.TotalDays} days ago")
            : when.ToString("d MMM yyyy");
    }

    private static string Quote(string? title) => title is null ? "" : $"“{title}”";
}
