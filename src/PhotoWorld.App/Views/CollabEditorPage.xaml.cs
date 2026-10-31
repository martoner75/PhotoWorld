using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class CollabEditorPage : ContentPage
{
    private readonly CollabEditorViewModel _viewModel;

    public CollabEditorPage(CollabEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Every time: the member may be back from adding the place they want to meet at.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadChoicesAsync();
    }
}
