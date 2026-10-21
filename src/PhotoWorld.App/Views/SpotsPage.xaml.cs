using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class SpotsPage : ContentPage
{
    private readonly SpotsViewModel _viewModel;
    private bool _loaded;

    public SpotsPage(SpotsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        viewModel.Loaded += (_, focus) =>
        {
            if (focus is not null) Map.MoveToRegion(MapSpan.FromCenterAndRadius(focus, Distance.FromKilometers(5)));
            else FitAll();
        };
        Map.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(25, 10), Distance.FromKilometers(6000)));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        await _viewModel.LoadAsync();
    }

    private async void OnPinInfoClicked(object? sender, PinClickedEventArgs e)
    {
        e.HideInfoWindow = true;
        if (sender is Pin { BindingContext: SpotGroup group }) await _viewModel.OpenAsync(group);
    }

    private void FitAll()
    {
        if (_viewModel.Spots.Count == 0) return;
        var lats = _viewModel.Spots.Select(s => s.Location.Latitude).ToList();
        var lngs = _viewModel.Spots.Select(s => s.Location.Longitude).ToList();
        var center = new Location((lats.Min() + lats.Max()) / 2, (lngs.Min() + lngs.Max()) / 2);
        Map.MoveToRegion(new MapSpan(center, Math.Min(Math.Max((lats.Max() - lats.Min()) * 1.4, 0.5), 170),
            Math.Min(Math.Max((lngs.Max() - lngs.Min()) * 1.4, 0.5), 360)));
    }
}
