using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class UploadPage : ContentPage
{
    private readonly UploadViewModel _viewModel;

    public UploadPage(UploadViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Places may have been added on the Places tab since last time.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadPlacesAsync();
    }
}
