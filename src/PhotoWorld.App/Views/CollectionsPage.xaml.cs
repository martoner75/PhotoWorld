using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class CollectionsPage : ContentPage
{
    private readonly CollectionsViewModel _viewModel;

    public CollectionsPage(CollectionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // Every time: coming back from a collection (deleted, photos removed) changes the list.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // "New" only on the member's own list (another member's page passes their id).
        this.SetToolbarItemVisible(NewItem, _viewModel.IsMine);
        _viewModel.EnsureLoaded();
    }
}
