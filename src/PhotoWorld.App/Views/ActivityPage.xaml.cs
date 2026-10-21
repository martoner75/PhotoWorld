using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class ActivityPage : ContentPage
{
    private readonly ActivityViewModel _viewModel;

    public ActivityPage(ActivityViewModel viewModel)
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
