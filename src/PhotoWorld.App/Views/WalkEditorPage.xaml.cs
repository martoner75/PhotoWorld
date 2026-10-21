using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class WalkEditorPage : ContentPage
{
    private readonly WalkEditorViewModel _viewModel;

    public WalkEditorPage(WalkEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        viewModel.PinMoved += (_, at) => ShowPin(at, zoom: true);
        Map.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(51.5, -0.12), Distance.FromKilometers(300)));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Pin is { } at) ShowPin(at, zoom: true);
    }

    private void OnMapClicked(object? sender, MapClickedEventArgs e) => _viewModel.SetPin(e.Location);

    private void ShowPin(Location at, bool zoom)
    {
        Map.Pins.Clear();
        Map.Pins.Add(new Pin { Location = at, Label = "Meeting point", Type = PinType.Place });
        if (zoom) Map.MoveToRegion(MapSpan.FromCenterAndRadius(at, Distance.FromMeters(800)));
    }
}
