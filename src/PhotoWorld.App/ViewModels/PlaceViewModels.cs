using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.App.ViewModels;

/// <summary>A saved place, shaped for the map pin and the list.</summary>
public sealed record PlaceItem(PlaceDto Place)
{
    public Location Location { get; } = new(Place.Latitude, Place.Longitude);
    public string Name => Place.Name;
    public string? Subtitle => Place.VisitedOn?.ToString("d MMMM yyyy");
    public string? Note => Place.Note;
}

/// <summary>One golden hour in the forecast list, in the phone's time zone.</summary>
public sealed record ForecastItem(LightWindowDto Window)
{
    public string When => $"{Window.Date:ddd d} · {(Window.Morning ? "morning" : "evening")} · {Window.Start.ToLocalTime():HH:mm}";
    public string Rating => LightForecast.Label(Window.Rating);
    public string Detail => LightForecast.Describe(Window);
}

/// <summary>The member's private map. Map placement (camera moves) is left to the page.</summary>
public partial class PlacesViewModel(PhotoWorldApi api) : ViewModelBase
{
    public ObservableCollection<PlaceItem> Places { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>Asks the page to show a spot on the map.</summary>
    public event EventHandler<Location>? FocusRequested;

    /// <summary>Asks the page to fit all pins in view.</summary>
    public event EventHandler? FitRequested;

    public Task LoadAsync() => RunAsync(async () =>
    {
        var places = await api.GetPlacesAsync();
        Places.Clear();
        foreach (var place in places.OrderByDescending(p => p.VisitedOn ?? DateOnly.FromDateTime(p.CreatedAt.Date)))
        {
            Places.Add(new PlaceItem(place));
        }
        IsEmpty = Places.Count == 0;
        FitRequested?.Invoke(this, EventArgs.Empty);
    });

    /// <summary>Finds a place by name with Apple's geocoder, then opens the editor there.</summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        var query = SearchText.Trim();
        if (query.Length == 0) return;
        try
        {
            var found = (await Geocoding.Default.GetLocationsAsync(query))?.FirstOrDefault();
            if (found is null)
            {
                await Ui.AlertAsync($"Couldn't find \"{query}\". Try another spelling, or tap the map.");
                return;
            }
            FocusRequested?.Invoke(this, found);
            await AddAtAsync(found, query);
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or InvalidOperationException or HttpRequestException)
        {
            await Ui.AlertAsync("Search isn't available right now. You can still tap the map to drop a pin.");
        }
    }

    /// <summary>Tapping the map (or a search result) starts a new place there.</summary>
    public Task AddAtAsync(Location location, string? name = null) =>
        Shell.Current.GoToAsync(Routes.PlaceEditor, new ShellNavigationQueryParameters
        {
            ["location"] = location,
            ["name"] = name ?? "",
        });

    [RelayCommand]
    private Task EditAsync(PlaceItem item) =>
        Shell.Current.GoToAsync(Routes.PlaceEditor, new ShellNavigationQueryParameters { ["place"] = item.Place });

    [RelayCommand]
    private void Show(PlaceItem item) => FocusRequested?.Invoke(this, item.Location);

    [RelayCommand]
    private Task DeleteAsync(PlaceItem item) => RunAsync(async () =>
    {
        if (!await Ui.ConfirmAsync("Remove place", $"Remove {item.Name} from your map?", "Remove")) return;
        await api.DeletePlaceAsync(item.Place.Id);
        Places.Remove(item);
        IsEmpty = Places.Count == 0;
    });
}

public partial class PlaceEditorViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid? _id;

    [ObservableProperty]
    public partial string Heading { get; set; } = "Add a place";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string Note { get; set; } = "";

    [ObservableProperty]
    public partial bool HasDate { get; set; }

    [ObservableProperty]
    public partial DateTime VisitedOn { get; set; } = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Coordinates))]
    public partial Location? Location { get; set; }

    [ObservableProperty]
    public partial bool ForecastAlerts { get; set; }

    /// <summary>The next few days of golden hours, for a saved place.</summary>
    public ObservableCollection<ForecastItem> Forecast { get; } = [];

    /// <summary>"Loading…", "isn't available", or empty once the forecast is shown.</summary>
    [ObservableProperty]
    public partial string ForecastMessage { get; set; } = "";

    public string ForecastAlertsHint =>
        $"An Activity alert (and a push) the evening before a clear or dramatic golden hour. Up to {PlaceLimits.MaxForecastAlerts} places.";

    public bool IsExisting => _id is not null;

    public DateTime Today => DateTime.Today;

    public string Coordinates => Location is null ? "" : $"{Location.Latitude:0.0000}, {Location.Longitude:0.0000}";

    public int NameMaxLength => PlaceLimits.NameMax;

    public int NoteMaxLength => PlaceLimits.NoteMax;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("place", out var value) && value is PlaceDto place)
        {
            _id = place.Id;
            Heading = "Edit place";
            (Name, Note) = (place.Name, place.Note ?? "");
            (HasDate, VisitedOn) = (place.VisitedOn is not null, place.VisitedOn?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today);
            Location = new Location(place.Latitude, place.Longitude);
            ForecastAlerts = place.ForecastAlerts;
            _ = LoadForecastAsync(place.Id);
        }
        else if (query.TryGetValue("location", out var loc) && loc is Location location)
        {
            Location = location;
            Name = query.TryGetValue("name", out var n) ? n as string ?? "" : "";
            if (Name.Length == 0) _ = SuggestNameAsync(location);
        }
        OnPropertyChanged(nameof(IsExisting));
    }

    /// <summary>"Kyoto, Japan" from Apple's reverse geocoder, unless the member has started typing.</summary>
    private async Task SuggestNameAsync(Location location)
    {
        try
        {
            var mark = (await Geocoding.Default.GetPlacemarksAsync(location))?.FirstOrDefault();
            var local = mark?.Locality ?? mark?.SubAdminArea ?? mark?.AdminArea;
            var suggestion = string.Join(", ", new[] { local, mark?.CountryName }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
            if (Name.Length == 0 && suggestion.Length > 0) Name = suggestion;
        }
        catch (Exception)
        {
            // Naming is a nicety; the member can type it.
        }
    }

    /// <summary>Shown under the form; a failure only says so, since the place itself is still editable.</summary>
    private async Task LoadForecastAsync(Guid id)
    {
        ForecastMessage = "Loading the light forecast…";
        try
        {
            var forecast = await api.GetPlaceForecastAsync(id);
            Forecast.Clear();
            foreach (var w in forecast.Windows) Forecast.Add(new ForecastItem(w));
            ForecastMessage = Forecast.Count == 0 ? "No golden hour here in the next few days." : "";
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            ForecastMessage = "The weather forecast isn't available right now.";
        }
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name) && Location is not null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var request = new SavePlaceRequest(Name.Trim(), string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
            Location!.Latitude, Location.Longitude, HasDate ? DateOnly.FromDateTime(VisitedOn) : null, ForecastAlerts);
        if (_id is { } id) await api.UpdatePlaceAsync(id, request);
        else await api.CreatePlaceAsync(request);
        await Shell.Current.GoToAsync("..");
    });

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async () =>
    {
        if (_id is not { } id || !await Ui.ConfirmAsync("Remove place", $"Remove {Name} from your map?", "Remove")) return;
        await api.DeletePlaceAsync(id);
        await Shell.Current.GoToAsync("..");
    });
}
