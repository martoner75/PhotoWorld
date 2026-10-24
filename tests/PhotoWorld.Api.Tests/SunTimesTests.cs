using PhotoWorld.Shared.Places;

namespace PhotoWorld.Api.Tests;

public class SunTimesTests
{
    private static void Near(DateTimeOffset expectedUtc, DateTimeOffset? actual, int minutes = 3)
    {
        Assert.NotNull(actual);
        Assert.True(Math.Abs((actual.Value - expectedUtc).TotalMinutes) <= minutes,
            $"Expected about {expectedUtc:u}, got {actual:u}");
    }

    [Fact]
    public void London_midsummer_matches_published_times()
    {
        // Greenwich, 21 June 2026: sunrise 04:43, sunset 21:21 BST (03:43 / 20:21 UTC).
        var day = SunTimes.For(new DateOnly(2026, 6, 21), 51.4769, -0.0005);
        Near(new DateTimeOffset(2026, 6, 21, 3, 43, 0, TimeSpan.Zero), day.Sunrise);
        Near(new DateTimeOffset(2026, 6, 21, 20, 21, 0, TimeSpan.Zero), day.Sunset);
    }

    [Fact]
    public void Western_and_southern_places_work_too()
    {
        // New York, 1 January 2026: sunrise 07:20, sunset 16:39 EST (12:20 / 21:39 UTC).
        var ny = SunTimes.For(new DateOnly(2026, 1, 1), 40.7128, -74.0060);
        Near(new DateTimeOffset(2026, 1, 1, 12, 20, 0, TimeSpan.Zero), ny.Sunrise);
        Near(new DateTimeOffset(2026, 1, 1, 21, 39, 0, TimeSpan.Zero), ny.Sunset);

        // Sydney, 1 January 2026: sunrise 05:48, sunset 20:10 AEDT (18:48 on 31 Dec / 09:10 UTC).
        var sydney = SunTimes.For(new DateOnly(2026, 1, 1), -33.8688, 151.2093);
        Near(new DateTimeOffset(2025, 12, 31, 18, 48, 0, TimeSpan.Zero), sydney.Sunrise);
        Near(new DateTimeOffset(2026, 1, 1, 9, 10, 0, TimeSpan.Zero), sydney.Sunset);
    }

    [Fact]
    public void Golden_and_blue_hours_wrap_sunrise_and_sunset_in_order()
    {
        var day = SunTimes.For(new DateOnly(2026, 3, 20), 45.4642, 9.19); // Milan, equinox
        Assert.NotNull(day.MorningBlue);
        Assert.NotNull(day.MorningGolden);
        Assert.NotNull(day.EveningGolden);
        Assert.NotNull(day.EveningBlue);

        Assert.Equal(day.MorningBlue!.End, day.MorningGolden!.Start);
        Assert.True(day.MorningGolden.Start < day.Sunrise && day.Sunrise < day.MorningGolden.End);
        Assert.True(day.EveningGolden!.Start < day.Sunset && day.Sunset < day.EveningGolden.End);
        Assert.Equal(day.EveningGolden.End, day.EveningBlue!.Start);
        // Near the equinox at mid latitudes each golden hour lasts roughly an hour.
        Assert.InRange((day.MorningGolden.End - day.MorningGolden.Start).TotalMinutes, 45, 75);
    }

    [Fact]
    public void A_walk_knows_which_light_it_catches()
    {
        var day = SunTimes.For(new DateOnly(2026, 6, 21), 51.4769, -0.0005);
        var evening = SunTimes.Overlaps(day, day.Sunset!.Value.AddHours(-1), day.Sunset.Value.AddMinutes(30));
        Assert.Equal(["evening golden hour", "evening blue hour"], evening);
        Assert.Empty(SunTimes.Overlaps(day, day.Sunrise!.Value.AddHours(5), day.Sunrise.Value.AddHours(6)));
    }

    [Fact]
    public void The_local_date_follows_the_longitude()
    {
        var lateUtc = new DateTimeOffset(2026, 1, 1, 22, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 1, 2), SunTimes.LocalDate(lateUtc, 151.2)); // Sydney: already the next morning
        Assert.Equal(new DateOnly(2026, 1, 1), SunTimes.LocalDate(lateUtc, -74.0)); // New York: still the evening
    }

    [Fact]
    public void Polar_days_and_nights_have_no_sunrise()
    {
        var midnightSun = SunTimes.For(new DateOnly(2026, 6, 21), 78.2232, 15.6267); // Longyearbyen
        Assert.Null(midnightSun.Sunrise);
        Assert.Null(midnightSun.Sunset);

        var polarNight = SunTimes.For(new DateOnly(2026, 12, 21), 78.2232, 15.6267);
        Assert.Null(polarNight.Sunrise);
        Assert.Null(polarNight.MorningGolden);
    }
}
