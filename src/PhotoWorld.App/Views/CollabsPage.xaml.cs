using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class CollabsPage : ContentPage
{
    private readonly CollabsViewModel _viewModel;

    public CollabsPage(CollabsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Every time: coming back from a call (replied, posted, cancelled) changes the list.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.StartAsync();
    }
}
