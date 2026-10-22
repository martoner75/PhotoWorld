using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

/// <summary>One number on the insights page, with its change against the previous 30 days.</summary>
public sealed record InsightTile(string Label, string Value, string Change, bool IsUp, bool IsDown);

/// <summary>The signed-in member's last 30 days: the same numbers as the website's Insights page.</summary>
public partial class InsightsViewModel(PhotoWorldApi api) : ViewModelBase
{
    [ObservableProperty] public partial IReadOnlyList<InsightTile> Tiles { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<InsightDayDto> Days { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<InsightPhotoDto> TopPhotos { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<InsightTagDto> TopTags { get; set; } = [];
    [ObservableProperty] public partial bool HasTopPhotos { get; set; }
    [ObservableProperty] public partial bool HasTopTags { get; set; }
    [ObservableProperty] public partial bool IsRefreshing { get; set; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try { await LoadAsync(); }
        finally { IsRefreshing = false; }
    }

    public Task LoadAsync() => RunAsync(async () =>
    {
        var i = await api.GetInsightsAsync();
        Tiles =
        [
            Tile("Views", i.Views), Tile("Likes", i.Likes), Tile("New followers", i.NewFollowers), Tile("Comments", i.Comments),
            new("Followers", Compact(i.Followers), $"{i.Photos} {(i.Photos == 1 ? "photo" : "photos")} shared", false, false),
        ];
        Days = i.Days;
        TopPhotos = i.TopPhotos;
        TopTags = i.TopTags;
        HasTopPhotos = TopPhotos.Count > 0;
        HasTopTags = TopTags.Count > 0;
    });

    [RelayCommand]
    private Task OpenPhotoAsync(InsightPhotoDto item) =>
        Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = item.Photo.Id });

    /// <summary>The arrow and sign carry the direction as well as the colour.</summary>
    private static InsightTile Tile(string label, InsightStatDto s)
    {
        if (s.Previous == 0)
            return new(label, Compact(s.Current), s.Current == 0 ? "No change" : "New this month", s.Current > 0, false);
        var change = (s.Current - s.Previous) * 100.0 / s.Previous;
        if (Math.Abs(change) < 0.5) return new(label, Compact(s.Current), "No change vs previous 30 days", false, false);
        return change > 0
            ? new(label, Compact(s.Current), $"▲ {change:0}% vs previous 30 days", true, false)
            : new(label, Compact(s.Current), $"▼ {-change:0}% vs previous 30 days", false, true);
    }

    private static string Compact(int n) => n >= 10_000 ? $"{n / 1000.0:0}K" : n >= 1_000 ? $"{n / 1000.0:0.#}K" : n.ToString("N0");
}
