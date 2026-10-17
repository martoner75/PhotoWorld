using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class PlaceEditorPage : ContentPage
{
    public PlaceEditorPage(PlaceEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
