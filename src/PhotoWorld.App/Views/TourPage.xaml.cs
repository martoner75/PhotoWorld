using PhotoWorld.App.Services;

namespace PhotoWorld.App.Views;

/// <summary>
/// The welcome tour: new members see it after their first sign-in (LoginViewModel); anyone can take it again from the
/// Me tab. Steps with a <see cref="TourStep.Tab"/> switch to that tab underneath; the tour ends on the tab it started on.
/// </summary>
public partial class TourPage : ContentPage
{
    private sealed record TourStep(string? Tab, string Title, string Text);

    private static readonly TourStep[] Steps =
    [
        new(null, "Welcome to PhotoCommunity", "A quick tour of the app. It takes under a minute."),
        new("gallery", "Gallery", "Photos shared by members, newest first. At the top: search opens Explore, ＋ lets you upload or create, and the inbox shows your Activity."),
        new("blogs", "Blogs", "Stories, trips and workshop write-ups from members. Following shows posts from the people you follow."),
        new("community", "Community", "Camera clubs, collabs to find people to shoot with, photo walks, online workshops and challenges: the places to meet other photographers and take part."),
        new("places", "Places", "Save your favourite spots and check the light before you go: sunrise, sunset and the weather."),
        new("me", "Me", "Your profile, collections, clubs, portfolio and insights, and Settings for your emails and light or dark mode. You can take this tour again from here."),
        new(null, "You're all set", "Why not start by sharing your first photo?"),
    ];

    private readonly ShellSection? _startTab = Shell.Current?.CurrentItem?.CurrentItem;
    private int _index;
    private bool _closing;

    public TourPage()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateArrow();
        Show();
    }

    public static Task StartAsync() => Shell.Current.Navigation.PushModalAsync(new TourPage(), animated: false);

    private void Show()
    {
        var step = Steps[_index];
        bool first = _index == 0, last = _index == Steps.Length - 1;
        CountLabel.Text = first || last ? "" : $"{_index} of {Steps.Length - 2}";
        CountLabel.IsVisible = !first && !last;
        TitleLabel.Text = step.Title;
        TextLabel.Text = step.Text;
        SkipButton.IsVisible = !last;
        BackButton.IsVisible = !first && !last;
        ShareButton.IsVisible = last;
        NextButton.Text = first ? "Show me around" : last ? "Done" : "Next";

        // A tab step: the note sits just above the tab bar, which is left undimmed. Otherwise the note is centred.
        if (step.Tab is { } tab) SelectTab(tab);
        Note.VerticalOptions = step.Tab is null ? LayoutOptions.Center : LayoutOptions.End;
        TabBarGap.Opacity = step.Tab is null ? 0.55 : 0;
        UpdateArrow();
    }

    /// <summary>
    /// On phones the tabs share the tab bar's width equally, so the arrow goes over the middle of the step's tab. The
    /// bar spans the screen on Android; since iOS 26 it floats, inset from both sides.
    /// </summary>
    private void UpdateArrow()
    {
        var tab = Steps[_index].Tab;
        var bar = Shell.Current?.CurrentItem;
        var position = tab is null || bar is null ? -1 : bar.Items.ToList().FindIndex(t => t.Route == tab);
        Arrow.IsVisible = position >= 0 && DeviceInfo.Idiom == DeviceIdiom.Phone && Width > 0;
        if (!Arrow.IsVisible) return;
        var inset = DeviceInfo.Platform == DevicePlatform.iOS && DeviceInfo.Version.Major >= 26 ? Width * 0.085 : 0;
        var centre = inset + (position + 0.5) * (Width - inset * 2) / bar!.Items.Count;
        Arrow.TranslationX = centre - Note.Padding.Left - 12; // the arrow glyph is about 24 wide
    }

    private static void SelectTab(string route)
    {
        if (Shell.Current?.CurrentItem is not { } bar) return;
        var tab = bar.Items.FirstOrDefault(t => t.Route == route);
        if (tab is not null && bar.CurrentItem != tab) bar.CurrentItem = tab;
    }

    private void OnNext(object? sender, EventArgs e)
    {
        if (_index == Steps.Length - 1) { _ = CloseAsync(); return; }
        _index++;
        Show();
    }

    private void OnBack(object? sender, EventArgs e)
    {
        if (_index == 0) return;
        _index--;
        Show();
    }

    private void OnSkip(object? sender, EventArgs e) => _ = CloseAsync();

    private async void OnShare(object? sender, EventArgs e)
    {
        await CloseAsync(backToStart: false);
        SelectTab("gallery");
        await Shell.Current.GoToAsync(Routes.Upload);
    }

    private async Task CloseAsync(bool backToStart = true)
    {
        if (_closing) return;
        _closing = true;
        if (backToStart && _startTab is not null && Shell.Current?.CurrentItem is { } bar && bar.Items.Contains(_startTab))
            bar.CurrentItem = _startTab;
        await Navigation.PopModalAsync(animated: false);
    }

    // Android's back button ends the tour instead of leaving the app.
    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }
}
