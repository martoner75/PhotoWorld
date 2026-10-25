using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class ClubEditorPage : ContentPage
{
    public ClubEditorPage(ClubEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
