using CommunityToolkit.Mvvm.Messaging;
using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class MemberPage : ContentPage
{
    private readonly MemberViewModel _viewModel;

    public MemberPage(MemberViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        // The member edited or deleted one of their photos: reload the grid when the page shows again.
        WeakReferenceMessenger.Default.Register<MemberViewModel, PhotoChangedMessage>(viewModel, (vm, _) => vm.Items.Clear());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.EnsureLoaded();
    }
}
