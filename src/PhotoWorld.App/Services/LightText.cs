using PhotoWorld.Shared.Places;

namespace PhotoWorld.App.Services;

/// <summary>Golden and blue hours (SunTimes, worked out on the phone) as a few lines in the phone's own time.</summary>
public static class LightText
{
    public static string Describe(SunDay day, IReadOnlyList<string>? overlaps = null)
    {
        if (day.Sunrise is null) return "The sun doesn't rise or set here that day.";
        static string T(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm");
        static string R(TimeRange? r) => r is null ? "–" : $"{T(r.Start)}–{T(r.End)}";
        return $"🔵 Blue hour {R(day.MorningBlue)} · 🟠 Golden hour {R(day.MorningGolden)}\n" +
               $"🌅 Sunrise {T(day.Sunrise.Value)} · Sunset {T(day.Sunset!.Value)}\n" +
               $"🟠 Golden hour {R(day.EveningGolden)} · 🔵 Blue hour {R(day.EveningBlue)}" +
               (overlaps is { Count: > 0 } ? $"\n✨ The walk catches the {string.Join(" and ", overlaps)}." : "");
    }
}
