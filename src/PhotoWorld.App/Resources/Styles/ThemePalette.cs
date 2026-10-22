namespace PhotoWorld.App;

/// <summary>
/// The app's two looks, like the website's: Gallery White, and Graphite for dark mode. <see cref="Apply"/> writes the
/// right one into the app's resources; pages use DynamicResource for these keys, so they switch with the device.
/// </summary>
public static class ThemePalette
{
    private static readonly Dictionary<string, Color> Light = new()
    {
        ["Ink"] = Color.FromArgb("#FFFFFF"), ["Ink2"] = Color.FromArgb("#FAFAF8"), ["Line"] = Color.FromArgb("#E5E3DE"),
        ["Text"] = Color.FromArgb("#17181B"), ["Muted"] = Color.FromArgb("#6B6F78"),
        ["Accent"] = Color.FromArgb("#C96F14"), ["AccentInk"] = Color.FromArgb("#FFFFFF"),
        ["AccentSoft"] = Color.FromArgb("#FBF1E4"), ["AccentLine"] = Color.FromArgb("#ECC89E"),
        ["Danger"] = Color.FromArgb("#B3261E"), ["DangerSoft"] = Color.FromArgb("#FBECEB"), ["DangerLine"] = Color.FromArgb("#E9B8B4"),
        ["Chart"] = Color.FromArgb("#C96F14"), ["Up"] = Color.FromArgb("#2E7D4F"),
        ["PhotoBackground"] = Color.FromArgb("#F1F0ED"), ["Scrim"] = Color.FromArgb("#E0FFFFFF"),
    };

    private static readonly Dictionary<string, Color> Dark = new()
    {
        ["Ink"] = Color.FromArgb("#1E2127"), ["Ink2"] = Color.FromArgb("#272B33"), ["Line"] = Color.FromArgb("#3A3F49"),
        ["Text"] = Color.FromArgb("#EEECE8"), ["Muted"] = Color.FromArgb("#A7ABB4"),
        ["Accent"] = Color.FromArgb("#F2A541"), ["AccentInk"] = Color.FromArgb("#1A1206"),
        ["AccentSoft"] = Color.FromArgb("#33291A"), ["AccentLine"] = Color.FromArgb("#6B5021"),
        ["Danger"] = Color.FromArgb("#F19999"), ["DangerSoft"] = Color.FromArgb("#3A1E1E"), ["DangerLine"] = Color.FromArgb("#6B2A2A"),
        ["Chart"] = Color.FromArgb("#CF7F22"), ["Up"] = Color.FromArgb("#6EC88C"),
        ["PhotoBackground"] = Color.FromArgb("#16181C"), ["Scrim"] = Color.FromArgb("#E01E2127"),
    };

    private const string PreferenceKey = "theme";

    /// <summary>The member's choice on this device: Light, Dark, or Unspecified (Automatic, follow the device).</summary>
    public static AppTheme Saved => Preferences.Default.Get(PreferenceKey, "auto") switch
    {
        "light" => AppTheme.Light,
        "dark" => AppTheme.Dark,
        _ => AppTheme.Unspecified,
    };

    /// <summary>Remembers the choice and switches straight away (the app's RequestedThemeChanged re-applies the palette).</summary>
    public static void Choose(AppTheme theme)
    {
        Preferences.Default.Set(PreferenceKey, theme switch { AppTheme.Light => "light", AppTheme.Dark => "dark", _ => "auto" });
        Application.Current!.UserAppTheme = theme;
        Chosen?.Invoke(null, theme);
    }

    /// <summary>A new choice from anywhere (top-bar icon or the Me tab), so every control showing it can follow.</summary>
    public static event EventHandler<AppTheme>? Chosen;

    public static void Apply(ResourceDictionary resources, AppTheme theme)
    {
        foreach (var (key, color) in theme == AppTheme.Dark ? Dark : Light) resources[key] = color;
    }
}
