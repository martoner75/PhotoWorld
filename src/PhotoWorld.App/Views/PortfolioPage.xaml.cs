using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class PortfolioPage : ContentPage
{
    private readonly PortfolioViewModel _viewModel;

    public PortfolioPage(PortfolioViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
