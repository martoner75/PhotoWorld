using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class PhotoEditorPage : ContentPage
{
    public PhotoEditorPage(PhotoEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
