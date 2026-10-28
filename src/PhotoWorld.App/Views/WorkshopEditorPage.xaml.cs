using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class WorkshopEditorPage : ContentPage
{
    public WorkshopEditorPage(WorkshopEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
