using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class CommunityPage : ContentPage
{
    private readonly CommunityViewModel _viewModel;

    public CommunityPage(CommunityViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Every time: joining a walk or entering a challenge elsewhere changes what's coming up.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Reload();
    }
}
