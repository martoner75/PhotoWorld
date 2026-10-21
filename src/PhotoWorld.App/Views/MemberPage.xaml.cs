using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class MemberPage : ContentPage
{
    private readonly MemberViewModel _viewModel;

    public MemberPage(MemberViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.EnsureLoaded();
    }
}
