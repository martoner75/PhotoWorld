using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class PostCommentsPage : ContentPage
{
    public PostCommentsPage(PostCommentsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
