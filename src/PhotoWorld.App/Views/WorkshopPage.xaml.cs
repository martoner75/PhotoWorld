using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class WorkshopPage : ContentPage
{
    private readonly WorkshopViewModel _viewModel;

    public WorkshopPage(WorkshopViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        this.SetToolbarItemVisible(EditItem, false);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkshopViewModel.Workshop))
                this.SetToolbarItemVisible(EditItem, viewModel.Workshop?.CanEdit == true);
        };
        // New messages go at the end: keep the newest in view.
        viewModel.MessagesAdded += (_, _) => Dispatcher.Dispatch(async () =>
            await Scroller.ScrollToAsync(0, Math.Max(0, Scroller.ContentSize.Height - Scroller.Height), true));
    }

    // Every time: back from editing, or the workshop changed meanwhile. The chat refreshes only while the page is shown.
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
