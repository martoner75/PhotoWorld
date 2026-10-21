using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class WalksPage : ContentPage
{
    private readonly WalksViewModel _viewModel;

    public WalksPage(WalksViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Every time: coming back from a walk (joined, organised, cancelled) changes the list.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Reload();
    }
}
