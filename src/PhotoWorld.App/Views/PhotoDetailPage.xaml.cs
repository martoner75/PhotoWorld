using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class PhotoDetailPage : ContentPage
{
    public PhotoDetailPage(PhotoDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // "Report" only for other members' photos, once we know whose it is.
        this.SetToolbarItemVisible(ReportItem, false);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PhotoDetailViewModel.Photo))
                this.SetToolbarItemVisible(ReportItem, viewModel.Photo?.CanReport == true);
        };
    }
}
