using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.Api.Services.Places;

/// <summary>When light-forecast alerts go out and what they say, kept apart so they're easy to test.</summary>
public static class ForecastRules
{
    /// <summary>Alerts go out in the early evening at the place: time to plan, before tomorrow's sunrise.</summary>
    public const int AlertLocalHour = 18;

    /// <summary>Sent-alert records are kept this long (enough to stop repeats), then deleted.</summary>
    public static readonly TimeSpan KeepNoticesFor = TimeSpan.FromDays(7);

    /// <summary>
    /// Whether <paramref name="now"/> is in the alert hour at a place, judged by its longitude like
    /// <see cref="SunTimes.LocalDate"/>. The job runs hourly, so each place gets one look a day.
    /// </summary>
    public static bool IsAlertHour(DateTimeOffset now, double longitude) =>
        now.UtcDateTime.AddHours(longitude / 15).Hour == AlertLocalHour;

    /// <summary>Tomorrow at the place.</summary>
    public static DateOnly Tomorrow(DateTimeOffset now, double longitude) => SunTimes.LocalDate(now, longitude).AddDays(1);

    /// <summary>The golden hour worth an alert: a dramatic one first, then a clear one, morning before evening. Null if neither is good.</summary>
    public static LightWindowDto? WorthTelling(IEnumerable<LightWindowDto> windows) =>
        windows.Where(w => w.Rating != LightRatings.Poor)
            .OrderBy(w => w.Rating == LightRatings.Dramatic ? 0 : 1)
            .ThenBy(w => w.Start)
            .FirstOrDefault();

    /// <summary>"Richmond Park · Sunset looks dramatic: 40% high cloud, clear horizon", cut to the snippet length.</summary>
    public static string Snippet(string placeName, LightWindowDto window) =>
        ActivityRules.Snippet($"{placeName} · {LightForecast.Describe(window)}")!;
}
