using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class ChallengesPage : ContentPage
{
    private readonly ChallengesViewModel _viewModel;

    public ChallengesPage(ChallengesViewModel viewModel)
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
