using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class BlogsPage : ContentPage
{
    private readonly BlogsViewModel _viewModel;

    public BlogsPage(BlogsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.EnsureLoaded();
    }
}
