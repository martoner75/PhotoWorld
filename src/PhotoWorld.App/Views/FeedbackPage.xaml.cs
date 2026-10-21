using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class FeedbackPage : ContentPage
{
    public FeedbackPage(FeedbackViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
