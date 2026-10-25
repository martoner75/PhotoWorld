namespace PhotoWorld.Shared.Places;

/// <summary>How a golden hour is likely to look. Stored nowhere; worked out from the weather forecast each time.</summary>
public static class LightRatings
{
    /// <summary>Little cloud anywhere: clean, warm light.</summary>
    public const string Clear = "clear";

    /// <summary>Clear near the horizon with mid or high cloud above, which the low sun lights up.</summary>
    public const string Dramatic = "dramatic";

    /// <summary>Low cloud or likely rain.</summary>
    public const string Poor = "poor";
}

/// <summary>One hour of forecast weather, in percent.</summary>
public sealed record HourlyWeather(DateTimeOffset Time, int LowCloud, int MidCloud, int HighCloud, int RainChance);

/// <summary>A morning or evening golden hour and how it's forecast to look. Times are UTC.</summary>
/// <param name="Morning">True for the morning golden hour, false for the evening one.</param>
public sealed record LightWindowDto(
    DateOnly Date,
    bool Morning,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Rating,
    int LowCloud,
    int MidCloud,
    int HighCloud,
    int RainChance);

/// <summary>The next few days of golden hours at one of the member's places.</summary>
public sealed record PlaceForecastDto(IReadOnlyList<LightWindowDto> Windows);

/// <summary>
/// Rates golden hours from an hourly cloud and rain forecast. A rule of thumb, not a promise: what photographers look
/// for is a clear horizon (little low cloud, so the sun gets through) and, for colour, some higher cloud for it to light.
/// </summary>
public static class LightForecast
{
    public const int DaysAhead = 3;

    /// <summary>Below this much low cloud the low sun usually gets through.</summary>
    public const int ClearHorizonBelow = 30;

    /// <summary>Mid or high cloud in this range catches the colour; more is usually a grey lid.</summary>
    public const int DramaticFrom = 30;
    public const int DramaticTo = 80;

    /// <summary>A rain chance from this level rates the hour as poor.</summary>
    public const int RainFrom = 30;

    public static string Rate(int lowCloud, int midCloud, int highCloud, int rainChance)
    {
        if (rainChance >= RainFrom || lowCloud >= ClearHorizonBelow) return LightRatings.Poor;
        var upper = Math.Max(midCloud, highCloud);
        return upper < DramaticFrom ? LightRatings.Clear
            : upper <= DramaticTo ? LightRatings.Dramatic
            : LightRatings.Poor;
    }

    /// <summary>
    /// The golden hour rated from the forecast hours it overlaps (each hour covers the 60 minutes after its time), or
    /// null when the forecast doesn't reach it.
    /// </summary>
    public static LightWindowDto? Rate(DateOnly date, bool morning, TimeRange window, IReadOnlyList<HourlyWeather> hours)
    {
        var overlapping = hours.Where(h => h.Time < window.End && window.Start < h.Time.AddHours(1)).ToList();
        if (overlapping.Count == 0) return null;

        int Average(Func<HourlyWeather, int> pick) => (int)Math.Round(overlapping.Average(pick));
        var (low, mid, high) = (Average(h => h.LowCloud), Average(h => h.MidCloud), Average(h => h.HighCloud));
        var rain = overlapping.Max(h => h.RainChance);
        return new LightWindowDto(date, morning, window.Start, window.End, Rate(low, mid, high, rain), low, mid, high, rain);
    }

    /// <summary>Morning and evening golden hours from <paramref name="from"/> for <paramref name="days"/> days, skipping any that have ended.</summary>
    public static IReadOnlyList<LightWindowDto> Windows(DateOnly from, int days, double latitude, double longitude,
        IReadOnlyList<HourlyWeather> hours, DateTimeOffset now)
    {
        var windows = new List<LightWindowDto>();
        for (var date = from; date < from.AddDays(days); date = date.AddDays(1))
        {
            var sun = SunTimes.For(date, latitude, longitude);
            foreach (var (morning, range) in new[] { (true, sun.MorningGolden), (false, sun.EveningGolden) })
            {
                if (range is null || range.End <= now) continue;
                if (Rate(date, morning, range, hours) is { } rated) windows.Add(rated);
            }
        }
        return windows;
    }

    /// <summary>"Sunset looks dramatic: 40% high cloud, clear horizon".</summary>
    public static string Describe(LightWindowDto w)
    {
        var when = w.Morning ? "Sunrise" : "Sunset";
        var upper = w.HighCloud >= w.MidCloud ? $"{w.HighCloud}% high cloud" : $"{w.MidCloud}% mid cloud";
        return w.Rating switch
        {
            LightRatings.Clear => $"{when} looks clear: {w.LowCloud}% low cloud",
            LightRatings.Dramatic => $"{when} looks dramatic: {upper}, clear horizon",
            _ when w.RainChance >= RainFrom => $"{when} looks poor: {w.RainChance}% chance of rain",
            _ => $"{when} looks poor: {Math.Max(w.LowCloud, Math.Max(w.MidCloud, w.HighCloud))}% cloud",
        };
    }

    /// <summary>A short label for the forecast strip: ☀️ Clear, 🌅 Dramatic, ☁️ Poor.</summary>
    public static string Label(string rating) => rating switch
    {
        LightRatings.Clear => "☀️ Clear",
        LightRatings.Dramatic => "🌅 Dramatic",
        _ => "☁️ Poor",
    };
}
