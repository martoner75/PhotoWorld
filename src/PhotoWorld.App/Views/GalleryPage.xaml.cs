using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class GalleryPage : ContentPage
{
    private readonly GalleryViewModel _viewModel;

    public GalleryPage(GalleryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.EnsureLoaded();
        _ = _viewModel.RefreshUnreadAsync();
    }
}
