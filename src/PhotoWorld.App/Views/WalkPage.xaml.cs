using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class WalkPage : ContentPage
{
    private readonly WalkViewModel _viewModel;

    public WalkPage(WalkViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        this.SetToolbarItemVisible(EditItem, false);
        viewModel.Loaded += (_, at) =>
        {
            Map.Pins.Clear();
            Map.Pins.Add(new Pin { Location = at, Label = viewModel.Walk?.Walk.MeetingPoint ?? "Meeting point", Type = PinType.Place });
            Map.MoveToRegion(MapSpan.FromCenterAndRadius(at, Distance.FromMeters(600)));
            this.SetToolbarItemVisible(EditItem, viewModel.Walk?.CanEdit == true);
        };
    }

    // Every time: back from editing, or the walk changed meanwhile.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
