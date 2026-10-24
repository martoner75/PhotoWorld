using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class CollectionEditorPage : ContentPage
{
    public CollectionEditorPage(CollectionEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
