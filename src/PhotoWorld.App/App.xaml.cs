namespace PhotoWorld.App;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        // Light (Gallery White) or dark (Graphite), following the device, like the website.
        ThemePalette.Apply(Resources, RequestedTheme);
        RequestedThemeChanged += (_, e) => ThemePalette.Apply(Resources, e.RequestedTheme);
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_shell);
}
