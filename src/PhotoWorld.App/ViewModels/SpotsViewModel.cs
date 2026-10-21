using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

/// <summary>The photos taken at one place: one pin on the Spots map.</summary>
public sealed record SpotGroup(Location Location, string Name, IReadOnlyList<PhotoSpotDto> Photos)
{
    public string Subtitle => Photos.Count == 1 ? $"“{Photos[0].Title}” by {Photos[0].OwnerName}" : $"{Photos.Count} photos";
}

public partial class SpotsViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid? _focus;

    public ObservableCollection<SpotGroup> Spots { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>Raised after loading: the place to show (the photo asked for), or null to fit every pin.</summary>
    public event EventHandler<Location?>? Loaded;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("photo", out var photo) && photo is Guid id) _focus = id;
    }

    public Task LoadAsync() => RunAsync(async () =>
    {
        var spots = await api.GetSpotsAsync();
        Spots.Clear();
        // Photos at the same coordinates share a pin.
        foreach (var group in spots.GroupBy(s => (Math.Round(s.Latitude, 5), Math.Round(s.Longitude, 5))))
        {
            var photos = group.ToList();
            Spots.Add(new SpotGroup(new Location(photos[0].Latitude, photos[0].Longitude), photos[0].PlaceName, photos));
        }
        IsEmpty = Spots.Count == 0;
        Loaded?.Invoke(this, Spots.FirstOrDefault(g => g.Photos.Any(p => p.PhotoId == _focus))?.Location);
    });

    /// <summary>Opens the photo at a pin; with several, asks which.</summary>
    public async Task OpenAsync(SpotGroup group)
    {
        var photo = group.Photos[0];
        if (group.Photos.Count > 1)
        {
            var labels = group.Photos.Take(20).Select((p, i) => $"{i + 1}. {p.Title} ({p.OwnerName})").ToArray();
            var choice = await Shell.Current.DisplayActionSheetAsync(group.Name, "Cancel", null, labels);
            var index = Array.IndexOf(labels, choice);
            if (index < 0) return;
            photo = group.Photos[index];
        }
        await Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = photo.PhotoId });
    }
}
