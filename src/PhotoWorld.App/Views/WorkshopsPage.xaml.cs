using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class WorkshopsPage : ContentPage
{
    private readonly WorkshopsViewModel _viewModel;

    public WorkshopsPage(WorkshopsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Every time: coming back from a workshop (joined, hosted, cancelled) changes the list.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Reload();
    }
}
