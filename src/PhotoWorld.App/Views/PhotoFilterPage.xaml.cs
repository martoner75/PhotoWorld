using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class PhotoFilterPage : ContentPage
{
    public PhotoFilterPage(PhotoFilterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
