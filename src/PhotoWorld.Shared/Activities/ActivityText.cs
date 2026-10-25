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
        var walk = Quote(a.WalkTitle);
        var club = Quote(a.ClubName);
        return a.Type switch
        {
            ActivityTypes.Like => $"{who} liked your photo {photo}",
            ActivityTypes.Comment => $"{who} commented on your photo {photo}",
            ActivityTypes.PostComment => $"{who} commented on your post {post}",
            ActivityTypes.Follow => $"{who} started following you",
            ActivityTypes.NewPhoto => $"{who} shared a new photo, {photo}",
            ActivityTypes.NewPost => $"{who} published a new post, {post}",
            ActivityTypes.WalkNew => $"{who} is organising a photo walk: {walk}",
            ActivityTypes.WalkJoined => $"{who} is coming on your photo walk {walk}",
            ActivityTypes.WalkChanged => $"The photo walk {walk} has changed: check the time and meeting point",
            ActivityTypes.WalkCancelled => $"The photo walk {walk} was cancelled",
            ActivityTypes.WalkReminder => $"Reminder: the photo walk {walk} is tomorrow",
            ActivityTypes.Feedback => $"{who} gave feedback on your photo {photo}",
            ActivityTypes.PhotoOfTheDay => $"Your photo {photo} is today's photo of the day! ⭐",
            ActivityTypes.PostApproved => $"Your post {post} was approved and is now published",
            // The admin's reason, if any, is the snippet, which every client shows after this text.
            ActivityTypes.PostRejected => $"Your post {post} wasn't approved. Edit it and submit it again",
            ActivityTypes.PhotoApproved => $"Your photo {photo} was approved and is now in the gallery",
            ActivityTypes.PhotoRejected => $"Your photo {photo} wasn't approved, so only you can see it",
            ActivityTypes.ClubRequest => $"{who} asked to join {club}",
            ActivityTypes.ClubAccepted => $"You're in! Your request to join {club} was accepted",
            ActivityTypes.ClubApproved => $"Your club {club} was approved and is now in the directory",
            ActivityTypes.ClubPhoto => $"{who} shared a new photo in {club}",
            ActivityTypes.ClubRejected => $"Your club {club} wasn't approved, so only you can see it",
            ActivityTypes.LightForecast => "Good light is forecast tomorrow at one of your places",
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
        [nameof(ActivitySettingsDto.Walks)] = "Photo walks: new ones by members you follow, people joining yours, changes and reminders",
        [nameof(ActivitySettingsDto.Forecasts)] = "Light forecasts: good golden hours at places you asked about",
        [nameof(ActivitySettingsDto.Clubs)] = "Clubs: requests to join clubs you run, and your requests being accepted",
        [nameof(ActivitySettingsDto.ClubPhotos)] = "New photos shared in your clubs (at most one alert per club a day)",
        [nameof(ActivitySettingsDto.Push)] = "Also send them to my phone (in the PhotoWorld app)",
    };

    /// <summary>
    /// The switches in sections, in the order the website's settings page and the app's Settings screen show them. A
    /// new kind of activity gets its label above and a place here, and both pick it up.
    /// </summary>
    public static readonly IReadOnlyList<(string Heading, IReadOnlyList<string> Settings)> SettingGroups =
    [
        ("Your photos and posts", [nameof(ActivitySettingsDto.Likes), nameof(ActivitySettingsDto.Comments)]),
        ("People", [nameof(ActivitySettingsDto.Follows), nameof(ActivitySettingsDto.FollowedPosts)]),
        ("Community", [nameof(ActivitySettingsDto.Challenges), nameof(ActivitySettingsDto.Walks),
            nameof(ActivitySettingsDto.Clubs), nameof(ActivitySettingsDto.ClubPhotos)]),
        ("Places", [nameof(ActivitySettingsDto.Forecasts)]),
        ("Delivery", [nameof(ActivitySettingsDto.Push)]),
    ];

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
