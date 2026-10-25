using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class ClubsPage : ContentPage
{
    private readonly ClubsViewModel _viewModel;

    public ClubsPage(ClubsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Every time: coming back from a club (joined, left, started one) changes the list.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Reload();
    }
}
