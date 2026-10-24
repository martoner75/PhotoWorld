using CommunityToolkit.Mvvm.Messaging;
using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class CollectionPage : ContentPage
{
    private readonly CollectionViewModel _viewModel;

    public CollectionPage(CollectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // "Edit" and "Delete" only on the member's own collections, once we know whose it is.
        this.SetToolbarItemVisible(EditItem, false);
        this.SetToolbarItemVisible(DeleteItem, false);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(CollectionViewModel.CanEdit)) return;
            this.SetToolbarItemVisible(EditItem, viewModel.CanEdit);
            this.SetToolbarItemVisible(DeleteItem, viewModel.CanEdit);
        };
        // Back from the editor: show the new name, description and visibility.
        WeakReferenceMessenger.Default.Register<CollectionViewModel, CollectionChangedMessage>(viewModel, (vm, m) =>
        {
            if (vm.Collection?.Id == m.Collection.Id) vm.Collection = m.Collection;
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.EnsureLoaded();
    }
}
