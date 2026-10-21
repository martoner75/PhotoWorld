using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class EnterChallengePage : ContentPage
{
    private readonly EnterChallengeViewModel _viewModel;

    public EnterChallengePage(EnterChallengeViewModel viewModel)
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
