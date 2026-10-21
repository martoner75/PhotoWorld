using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.View;

namespace PhotoWorld.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        UpdateStatusBar();
    }

    // UiMode is handled here (not by restarting), so follow light/dark switches too.
    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        UpdateStatusBar();
    }

    /// <summary>Dark status-bar icons on the light theme, light ones on the dark theme.</summary>
    private void UpdateStatusBar()
    {
        if (Window is not { } window) return;
        var night = (Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
        if (WindowCompat.GetInsetsController(window, window.DecorView) is { } bars) bars.AppearanceLightStatusBars = !night;
    }
}
