namespace PhotoWorld.Shared.Challenges;

/// <summary>Challenge wording shared by the website and the app.</summary>
public static class ChallengeDisplay
{
    /// <summary>"3 days left", "Starts 1 Nov 2026", "Ended 30 Nov 2026". Dates show in <paramref name="zone"/> (UTC when null).</summary>
    public static string When(this ChallengeSummaryDto c, DateTimeOffset now, TimeZoneInfo? zone = null) => c.Phase switch
    {
        ChallengePhase.Upcoming => $"Starts {In(c.StartsAt, zone):d MMM yyyy}",
        ChallengePhase.Open => TimeLeft(c.EndsAt - now),
        _ => $"Ended {In(c.EndsAt, zone):d MMM yyyy}",
    };

    public static string TimeLeft(TimeSpan left) =>
        left.TotalDays >= 2 ? $"{(int)left.TotalDays} days left"
        : left.TotalHours >= 2 ? $"{(int)left.TotalHours} hours left"
        : left.TotalMinutes >= 2 ? $"{(int)left.TotalMinutes} minutes left"
        : "Closing now";

    public static string Votes(int votes) => votes == 1 ? "1 vote" : $"{votes} votes";

    public static string PhaseLabel(ChallengePhase phase) => phase switch
    {
        ChallengePhase.Open => "Open",
        ChallengePhase.Upcoming => "Coming up",
        _ => "Ended",
    };

    private static DateTimeOffset In(DateTimeOffset time, TimeZoneInfo? zone) =>
        zone is null ? time : TimeZoneInfo.ConvertTime(time, zone);
}
