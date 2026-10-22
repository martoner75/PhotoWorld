using System.Security.Cryptography;
using System.Text;
using PhotoWorld.Shared.Notifications;

namespace PhotoWorld.Api.Services.Notifications;

/// <summary>The pure rules behind notifications, kept apart so they're easy to test.</summary>
public static class NotificationRules
{
    public static readonly TimeSpan ConfirmLinkLifetime = TimeSpan.FromDays(2);

    /// <summary>
    /// Posts newer than this are left for the next run: a post's PublishedAt is stamped just before its row is
    /// committed, so a cut-off of "now" could slip past a post that commits a moment later and never send it.
    /// </summary>
    public static readonly TimeSpan PublishSettleTime = TimeSpan.FromMinutes(1);

    /// <summary>Most posts listed in one email; the rest are summed up with a link to the blogs page.</summary>
    public const int MaxPostsPerEmail = 20;

    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static TimeSpan Period(string frequency) => frequency switch
    {
        NotificationFrequency.Daily => TimeSpan.FromDays(1),
        NotificationFrequency.Weekly => TimeSpan.FromDays(7),
        _ => TimeSpan.Zero,
    };

    /// <summary>Instant subscribers are checked every run; digests once their period has passed.</summary>
    public static bool IsDue(string frequency, DateTimeOffset periodStartedAt, DateTimeOffset now) =>
        now >= periodStartedAt + Period(frequency);

    public static readonly TimeSpan WeeklySummaryPeriod = TimeSpan.FromDays(7);

    /// <summary>The verified Google address needs no confirmation link; any other address does.</summary>
    public static bool IsAccountEmail(string email, string? accountEmail) =>
        accountEmail is not null && string.Equals(email.Trim(), accountEmail.Trim(), StringComparison.OrdinalIgnoreCase);
}
