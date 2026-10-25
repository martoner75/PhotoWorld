using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    /// <summary>The Me tab's view model (one for the app), so both screens show the same, freshly loaded settings.</summary>
    public SettingsPage(ProfileViewModel viewModel)
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
