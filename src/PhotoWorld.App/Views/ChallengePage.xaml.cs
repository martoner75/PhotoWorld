using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class ChallengePage : ContentPage
{
    private readonly ChallengeViewModel _viewModel;

    public ChallengePage(ChallengeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Every time: coming back from a photo (a new like) or from entering one changes the standings.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
