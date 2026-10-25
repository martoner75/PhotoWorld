using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Api.Services.Places;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.Api.Tests;

public class LightForecastTests
{
    private static readonly DateOnly Day = new(2026, 11, 2);
    private static readonly DateTimeOffset Midnight = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

    // Richmond Park, London.
    private const double Lat = 51.44, Lon = -0.27;

    [Theory]
    [InlineData(5, 10, 10, 0, LightRatings.Clear)]
    [InlineData(10, 20, 45, 10, LightRatings.Dramatic)]
    [InlineData(10, 60, 20, 0, LightRatings.Dramatic)]
    [InlineData(10, 20, 95, 0, LightRatings.Poor)] // a grey lid
    [InlineData(40, 0, 0, 0, LightRatings.Poor)] // low cloud blocks the sun
    [InlineData(5, 10, 40, 30, LightRatings.Poor)] // likely rain
    public void Golden_hours_are_rated_from_cloud_and_rain(int low, int mid, int high, int rain, string expected)
    {
        Assert.Equal(expected, LightForecast.Rate(low, mid, high, rain));
    }

    [Fact]
    public void A_window_averages_the_hours_it_overlaps_and_takes_the_worst_rain()
    {
        var window = new TimeRange(Midnight.AddHours(15).AddMinutes(40), Midnight.AddHours(16).AddMinutes(30));
        var hours = new[]
        {
            Hour(14, low: 90, mid: 0, high: 0, rain: 90), // ends before the window
            Hour(15, low: 10, mid: 0, high: 40, rain: 0),
            Hour(16, low: 20, mid: 0, high: 60, rain: 20),
            Hour(17, low: 90, mid: 0, high: 0, rain: 90), // starts after it
        };

        var rated = LightForecast.Rate(Day, morning: false, window, hours)!;

        Assert.Equal((15, 0, 50, 20), (rated.LowCloud, rated.MidCloud, rated.HighCloud, rated.RainChance));
        Assert.Equal(LightRatings.Dramatic, rated.Rating);
    }

    [Fact]
    public void A_window_beyond_the_forecast_is_left_out()
    {
        var window = new TimeRange(Midnight.AddDays(5), Midnight.AddDays(5).AddHours(1));
        Assert.Null(LightForecast.Rate(Day, true, window, [Hour(10, 0, 0, 0, 0)]));
    }

    [Fact]
    public void Windows_cover_both_golden_hours_a_day_and_skip_ones_already_over()
    {
        var hours = Enumerable.Range(0, 24 * 4).Select(h => Hour(h, 5, 5, 5, 0)).ToList();

        var all = LightForecast.Windows(Day, 3, Lat, Lon, hours, Midnight);
        Assert.Equal(6, all.Count);
        Assert.All(all, w => Assert.Equal(LightRatings.Clear, w.Rating));

        var afternoon = LightForecast.Windows(Day, 3, Lat, Lon, hours, Midnight.AddHours(12));
        Assert.Equal(5, afternoon.Count);
        Assert.False(afternoon[0].Morning);
    }

    [Fact]
    public void Descriptions_say_why()
    {
        Assert.Equal("Sunset looks dramatic: 45% high cloud, clear horizon",
            LightForecast.Describe(Window(LightRatings.Dramatic, morning: false, low: 10, mid: 20, high: 45, rain: 0)));
        Assert.Equal("Sunrise looks clear: 5% low cloud", LightForecast.Describe(Window(LightRatings.Clear, true, 5, 0, 0, 0)));
        Assert.Equal("Sunrise looks poor: 60% chance of rain", LightForecast.Describe(Window(LightRatings.Poor, true, 5, 0, 0, 60)));
    }

    [Theory]
    [InlineData(17, 59, 0, false)]
    [InlineData(18, 0, 0, true)]
    [InlineData(18, 59, 0, true)]
    [InlineData(19, 0, 0, false)]
    [InlineData(9, 0, 135, true)] // 09:00 UTC is 18:00 in Japan by longitude
    public void Alerts_go_out_in_the_early_evening_at_the_place(int hour, int minute, double longitude, bool expected)
    {
        Assert.Equal(expected, ForecastRules.IsAlertHour(Midnight.AddHours(hour).AddMinutes(minute), longitude));
    }

    [Fact]
    public void Tomorrow_is_judged_at_the_place()
    {
        var lateUtc = Midnight.AddHours(23);
        Assert.Equal(Day.AddDays(1), ForecastRules.Tomorrow(lateUtc, 0));
        Assert.Equal(Day.AddDays(2), ForecastRules.Tomorrow(lateUtc, 150)); // already the 3rd in eastern Australia
    }

    [Fact]
    public void A_dramatic_golden_hour_beats_a_clear_one_and_poor_ones_are_never_told()
    {
        var clearMorning = Window(LightRatings.Clear, true, 5, 0, 0, 0);
        var dramaticEvening = Window(LightRatings.Dramatic, false, 5, 0, 50, 0);
        var poor = Window(LightRatings.Poor, true, 80, 0, 0, 0);

        Assert.Equal(dramaticEvening, ForecastRules.WorthTelling([clearMorning, dramaticEvening]));
        Assert.Equal(clearMorning, ForecastRules.WorthTelling([poor, clearMorning]));
        Assert.Null(ForecastRules.WorthTelling([poor]));
    }

    [Fact]
    public void The_alert_names_the_place_and_fits_the_snippet()
    {
        var w = Window(LightRatings.Dramatic, false, 5, 0, 50, 0);
        Assert.Equal("Richmond Park · Sunset looks dramatic: 50% high cloud, clear horizon", ForecastRules.Snippet("Richmond Park", w));
        Assert.True(ForecastRules.Snippet(new string('x', 200), w).Length <= ActivityRules.SnippetMax + 1);
    }

    [Fact]
    public void Light_forecast_activity_follows_its_own_switch()
    {
        Assert.True(ActivityRules.Wants(ActivitySettingsDto.AllOn, ActivityTypes.LightForecast));
        Assert.False(ActivityRules.Wants(ActivitySettingsDto.AllOn with { Forecasts = false }, ActivityTypes.LightForecast));
        Assert.Contains(ActivityTypes.LightForecast, ActivityTypes.All);
    }

    [Fact]
    public void Open_Meteo_hourly_columns_are_read_and_gaps_count_as_zero()
    {
        const string json = """
            {"hourly":{"time":[1793577600,1793581200],
              "cloud_cover_low":[12,null],"cloud_cover_mid":[30.4,0],"cloud_cover_high":[101,55],
              "precipitation_probability":[null,40]}}
            """;

        var hours = OpenMeteoClient.Parse(json);

        Assert.Equal(2, hours.Count);
        Assert.Equal(new HourlyWeather(DateTimeOffset.FromUnixTimeSeconds(1793577600), 12, 30, 100, 0), hours[0]);
        Assert.Equal(new HourlyWeather(DateTimeOffset.FromUnixTimeSeconds(1793581200), 0, 0, 55, 40), hours[1]);
    }

    [Fact]
    public void An_unreadable_Open_Meteo_answer_is_reported_as_unavailable()
    {
        Assert.Throws<ForecastUnavailableException>(() => OpenMeteoClient.Parse("""{"error":true,"reason":"bad"}"""));
        Assert.Throws<ForecastUnavailableException>(() => OpenMeteoClient.Parse("not json"));
    }

    private static HourlyWeather Hour(int hour, int low, int mid, int high, int rain) =>
        new(Midnight.AddHours(hour), low, mid, high, rain);

    private static LightWindowDto Window(string rating, bool morning, int low, int mid, int high, int rain) =>
        new(Day, morning, Midnight.AddHours(morning ? 7 : 16), Midnight.AddHours(morning ? 8 : 17), rating, low, mid, high, rain);
}
