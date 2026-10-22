using System.ComponentModel.DataAnnotations;

namespace PhotoWorld.Shared.Notifications;

/// <summary>How often a subscriber hears about new blog posts.</summary>
public static class NotificationFrequency
{
    public const string Instant = "instant";
    public const string Daily = "daily";
    public const string Weekly = "weekly";

    /// <summary>Stored value → label shown in the settings.</summary>
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Instant] = "As soon as a post is published",
        [Daily] = "Once a day (digest)",
        [Weekly] = "Once a week (digest)",
    };

    public static bool IsValid(string? value) => value is not null && Labels.ContainsKey(value);
}

public static class NotificationLimits
{
    public const int EmailMax = 320;
}

/// <summary>
/// A member's email settings. <see cref="Email"/> is null until they first save; <see cref="AccountEmail"/> is their
/// verified Google address, offered as the default. <see cref="Enabled"/> is for new blog posts; <see cref="WeeklySummary"/>
/// is the weekly email about their own activity. Either goes out only once the address is <see cref="Confirmed"/>.
/// </summary>
public sealed record EmailNotificationsDto(
    string? Email,
    string Frequency,
    bool Enabled,
    bool Confirmed,
    string? AccountEmail,
    bool WeeklySummary = false);

public sealed record SaveEmailNotificationsRequest(
    [Required, EmailAddress, StringLength(NotificationLimits.EmailMax)] string Email,
    [Required] string Frequency,
    bool Enabled,
    bool WeeklySummary = false);

/// <summary>The secret from a confirmation or unsubscribe link.</summary>
public sealed record NotificationTokenRequest([Required, StringLength(100)] string Token);
