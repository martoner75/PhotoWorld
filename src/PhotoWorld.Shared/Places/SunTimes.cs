namespace PhotoWorld.Shared.Places;

/// <summary>A stretch of a day, in UTC.</summary>
public sealed record TimeRange(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// The light photographers plan around on one day at one place, in UTC. Any part is null when the sun doesn't
/// reach that height that day (polar summer or winter).
/// </summary>
/// <param name="MorningBlue">Blue hour: the sun between 6° and 4° below the horizon.</param>
/// <param name="MorningGolden">Golden hour: from 4° below the horizon to 6° above it.</param>
public sealed record SunDay(
    DateOnly Date,
    DateTimeOffset? Sunrise,
    DateTimeOffset? Sunset,
    TimeRange? MorningBlue,
    TimeRange? MorningGolden,
    TimeRange? EveningGolden,
    TimeRange? EveningBlue);

/// <summary>
/// Sunrise, sunset, golden and blue hours from the sun's position (the NOAA "sunrise equation"; within a minute or
/// two away from the poles). Worked out on the spot: no outside service is asked, so nothing about the place leaves.
/// </summary>
public static class SunTimes
{
    /// <summary>The usual sunrise/sunset height: the sun's upper edge on the horizon, allowing for refraction.</summary>
    public const double HorizonDegrees = -0.833;
    public const double GoldenTopDegrees = 6;
    public const double BlueTopDegrees = -4;
    public const double BlueBottomDegrees = -6;

    /// <param name="date">The day at the place (its local calendar date).</param>
    /// <param name="longitude">Degrees east (west is negative).</param>
    public static SunDay For(DateOnly date, double latitude, double longitude)
    {
        var sunrise = Crossing(date, latitude, longitude, HorizonDegrees);
        var golden = Crossing(date, latitude, longitude, GoldenTopDegrees);
        var blueTop = Crossing(date, latitude, longitude, BlueTopDegrees);
        var blueBottom = Crossing(date, latitude, longitude, BlueBottomDegrees);

        return new SunDay(date, sunrise?.Rise, sunrise?.Set,
            MorningBlue: Range(blueBottom?.Rise, blueTop?.Rise),
            MorningGolden: Range(blueTop?.Rise, golden?.Rise),
            EveningGolden: Range(golden?.Set, blueTop?.Set),
            EveningBlue: Range(blueTop?.Set, blueBottom?.Set));
    }

    /// <summary>
    /// The calendar date at a place for a moment, judged by its longitude (15° per hour). Good enough to pick the right
    /// day for the light without a time-zone database.
    /// </summary>
    public static DateOnly LocalDate(DateTimeOffset moment, double longitude) =>
        DateOnly.FromDateTime(moment.UtcDateTime.AddHours(longitude / 15));

    /// <summary>Which of the day's golden and blue hours overlap <paramref name="start"/>–<paramref name="end"/>.</summary>
    public static IReadOnlyList<string> Overlaps(SunDay day, DateTimeOffset start, DateTimeOffset end)
    {
        var parts = new (string Name, TimeRange? Range)[]
        {
            ("morning blue hour", day.MorningBlue), ("morning golden hour", day.MorningGolden),
            ("evening golden hour", day.EveningGolden), ("evening blue hour", day.EveningBlue),
        };
        return parts.Where(p => p.Range is { } r && r.Start < end && start < r.End).Select(p => p.Name).ToList();
    }

    private static TimeRange? Range(DateTimeOffset? start, DateTimeOffset? end) =>
        start is { } s && end is { } e && e > s ? new TimeRange(s, e) : null;

    /// <summary>When the sun's centre passes <paramref name="altitude"/> going up and coming down, or null if it doesn't.</summary>
    private static (DateTimeOffset Rise, DateTimeOffset Set)? Crossing(DateOnly date, double latitude, double longitude, double altitude)
    {
        const double j2000 = 2451545.0;
        var julianNoon = date.DayNumber - new DateOnly(2000, 1, 1).DayNumber + j2000; // JD at 12:00 UTC that date
        var n = Math.Round(julianNoon - j2000 + 0.0008);
        var meanSolarNoon = n - longitude / 360;
        var anomaly = Normalize(357.5291 + 0.98560028 * meanSolarNoon);
        var m = Radians(anomaly);
        var centre = 1.9148 * Math.Sin(m) + 0.0200 * Math.Sin(2 * m) + 0.0003 * Math.Sin(3 * m);
        var eclipticLongitude = Radians(Normalize(anomaly + centre + 180 + 102.9372));
        var transit = j2000 + meanSolarNoon + 0.0053 * Math.Sin(m) - 0.0069 * Math.Sin(2 * eclipticLongitude);
        var declination = Math.Asin(Math.Sin(eclipticLongitude) * Math.Sin(Radians(23.4397)));

        var phi = Radians(latitude);
        var cosHourAngle = (Math.Sin(Radians(altitude)) - Math.Sin(phi) * Math.Sin(declination))
                           / (Math.Cos(phi) * Math.Cos(declination));
        if (cosHourAngle is < -1 or > 1) return null;
        var hourAngle = Math.Acos(cosHourAngle) * 180 / Math.PI;

        return (FromJulian(transit - hourAngle / 360), FromJulian(transit + hourAngle / 360));
    }

    private static DateTimeOffset FromJulian(double julianDay) =>
        new DateTimeOffset(2000, 1, 1, 12, 0, 0, TimeSpan.Zero).AddDays(julianDay - 2451545.0);

    private static double Radians(double degrees) => degrees * Math.PI / 180;
    private static double Normalize(double degrees) => ((degrees % 360) + 360) % 360;
}
