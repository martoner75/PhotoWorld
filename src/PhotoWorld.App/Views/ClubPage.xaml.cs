using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class ClubPage : ContentPage
{
    private readonly ClubViewModel _viewModel;

    public ClubPage(ClubViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        this.SetToolbarItemVisible(EditItem, false);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClubViewModel.Club)) this.SetToolbarItemVisible(EditItem, viewModel.Club?.CanEdit == true);
        };
    }

    // Every time: back from editing, or the club changed meanwhile.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
