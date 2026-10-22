namespace PhotoWorld.App.Views;

/// <summary>
/// The light/dark switch in a page's top bar, like the website's header: a moon in light mode, a sun in dark mode.
/// One tap saves the opposite theme (Automatic is on the Me tab).
/// </summary>
public class ThemeToolbarItem : ToolbarItem
{
    public ThemeToolbarItem()
    {
        Order = ToolbarItemOrder.Primary;
        Priority = 10;
        Clicked += (_, _) => ThemePalette.Choose(IsDark ? AppTheme.Light : AppTheme.Dark);
        Application.Current!.RequestedThemeChanged += (_, _) => Update();
        Update();
    }

    private static bool IsDark => Application.Current!.RequestedTheme == AppTheme.Dark;

    private void Update()
    {
        IconImageSource = IsDark ? "icon_sun.png" : "icon_moon.png";
        Text = IsDark ? "Switch to light mode" : "Switch to dark mode";
        SemanticProperties.SetDescription(this, Text);
    }
}
