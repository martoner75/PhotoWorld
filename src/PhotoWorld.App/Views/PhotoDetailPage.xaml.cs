using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class PhotoDetailPage : ContentPage
{
    public PhotoDetailPage(PhotoDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
