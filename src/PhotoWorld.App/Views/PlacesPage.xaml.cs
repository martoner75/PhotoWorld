using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using PhotoWorld.App.ViewModels;

namespace PhotoWorld.App.Views;

public partial class PlacesPage : ContentPage
{
    private readonly PlacesViewModel _viewModel;
    private bool _wide;

    public PlacesPage(PlacesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        viewModel.FocusRequested += (_, location) => Map.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(25)));
        viewModel.FitRequested += (_, _) => FitAll();
        Map.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(25, 10), Distance.FromKilometers(6000)));
    }

    // Comes back here after adding or editing a place, so reload every time.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnMapClicked(object? sender, MapClickedEventArgs e) => await _viewModel.AddAtAsync(e.Location);

    private void OnPinInfoClicked(object? sender, PinClickedEventArgs e)
    {
        e.HideInfoWindow = true;
        if (sender is Pin { BindingContext: PlaceItem item }) _viewModel.EditCommand.Execute(item);
    }

    private void FitAll()
    {
        if (_viewModel.Places.Count == 0) return;
        var lats = _viewModel.Places.Select(p => p.Location.Latitude).ToList();
        var lngs = _viewModel.Places.Select(p => p.Location.Longitude).ToList();
        var center = new Location((lats.Min() + lats.Max()) / 2, (lngs.Min() + lngs.Max()) / 2);
        // Pad by 40% so edge pins aren't under the screen edge; never zoom closer than a city.
        var latSpan = Math.Max((lats.Max() - lats.Min()) * 1.4, 0.5);
        var lngSpan = Math.Max((lngs.Max() - lngs.Min()) * 1.4, 0.5);
        Map.MoveToRegion(new MapSpan(center, Math.Min(latSpan, 170), Math.Min(lngSpan, 360)));
    }

    /// <summary>On a wide screen (iPad, landscape) put the list beside the map instead of under it.</summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        var wide = width > 700 && width > height * 0.9;
        if (wide == _wide) return;
        _wide = wide;

        Root.RowDefinitions = wide
            ? [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)]
            : [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Star)];
        Root.ColumnDefinitions = wide
            ? [new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(360))]
            : [new ColumnDefinition(GridLength.Star)];

        Grid.SetColumnSpan(Map, 1);
        Grid.SetRow(Map, 1);
        Grid.SetRow(List, wide ? 1 : 2);
        Grid.SetColumn(List, wide ? 1 : 0);
    }
}
