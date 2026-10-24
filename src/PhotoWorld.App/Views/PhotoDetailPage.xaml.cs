using CommunityToolkit.Mvvm.Messaging;
using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class PhotoDetailPage : ContentPage
{
    public PhotoDetailPage(PhotoDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // "Report" only for other members' photos and "Edit" only for the member's own, once we know whose it is.
        this.SetToolbarItemVisible(ReportItem, false);
        this.SetToolbarItemVisible(EditItem, false);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(PhotoDetailViewModel.Photo)) return;
            this.SetToolbarItemVisible(ReportItem, viewModel.Photo?.CanReport == true);
            this.SetToolbarItemVisible(EditItem, viewModel.Photo?.CanEdit == true);
        };

        // Back from the editor: show what changed.
        WeakReferenceMessenger.Default.Register<PhotoDetailViewModel, PhotoChangedMessage>(viewModel, (vm, m) => vm.OnPhotoChanged(m));
    }
}
