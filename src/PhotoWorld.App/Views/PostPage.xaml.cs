using PhotoWorld.App.Services;
using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class PostPage : ContentPage
{
    public PostPage(PostViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // "Report" only for other members' posts, once we know whose it is.
        this.SetToolbarItemVisible(ReportItem, false);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PostViewModel.Post))
                this.SetToolbarItemVisible(ReportItem, viewModel.Post?.CanReport == true);
        };
    }

    /// <summary>The post renders in place; any link the member taps opens in the browser instead.</summary>
    private async void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "mailto")) return;
        e.Cancel = true;
        if (uri.Scheme == "mailto") await Launcher.Default.OpenAsync(uri);
        else await Ui.OpenInBrowserAsync(uri);
    }
}
