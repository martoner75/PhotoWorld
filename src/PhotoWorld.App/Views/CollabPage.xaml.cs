using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class CollabPage : ContentPage
{
    private readonly CollabViewModel _viewModel;

    public CollabPage(CollabViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        this.SetToolbarItemVisible(EditItem, false);
        // Only the poster edits: the meeting point and photos come from their own places and photos.
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CollabViewModel.Collab))
                this.SetToolbarItemVisible(EditItem, viewModel.Collab?.Collab.IsPoster == true);
        };
        viewModel.MessagesAdded += (_, _) => Dispatcher.Dispatch(async () =>
            await Scroller.ScrollToAsync(0, Math.Max(0, Scroller.ContentSize.Height - Scroller.Height), true));
    }

    // Every time: back from editing, or the call changed meanwhile. The chat refreshes only while the page is shown.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        _viewModel.StartChat(Dispatcher);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopChat();
    }
}
