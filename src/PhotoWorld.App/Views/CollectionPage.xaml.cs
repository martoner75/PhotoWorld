using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class CollectionPage : ContentPage
{
    private readonly CollectionViewModel _viewModel;

    public CollectionPage(CollectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // "Delete" only on the member's own collections, once we know whose it is.
        this.SetToolbarItemVisible(DeleteItem, false);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CollectionViewModel.CanEdit)) this.SetToolbarItemVisible(DeleteItem, viewModel.CanEdit);
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.EnsureLoaded();
    }
}
