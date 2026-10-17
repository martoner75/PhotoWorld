namespace PhotoWorld.App;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Dark; // PhotoWorld is dark-only, like the website
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_shell);
}
