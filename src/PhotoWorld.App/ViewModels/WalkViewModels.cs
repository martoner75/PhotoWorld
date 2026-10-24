using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Places;
using PhotoWorld.Shared.Walks;

namespace PhotoWorld.App.ViewModels;

public sealed record WalkItem(WalkSummaryDto Walk)
{
    public string When => Walk.StartsAt.ToLocalTime().ToString("ddd d MMM, HH:mm");
    public string Facts => $"📍 {Walk.MeetingPoint} · {Walk.AttendeeCount}{(Walk.MaxPeople is { } m ? $"/{m}" : "")} going";
    public string? Badge => Walk.IsCancelled ? "Cancelled" : Walk.IsOrganizer ? "Organising" : Walk.IsGoing ? "Going" : null;
}

public partial class WalksViewModel(PhotoWorldApi api) : ViewModelBase
{
    public ObservableCollection<WalkItem> Items { get; } = [];

    [ObservableProperty] public partial string List { get; set; } = WalkLists.Upcoming;
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    public string EmptyMessage => List switch
    {
        WalkLists.Mine => "You're not organising or going on any walks yet.",
        WalkLists.Past => "No past walks yet.",
        _ => "No walks coming up. Why not organise the first one?",
    };

    public void Reload() { if (!IsRefreshing) IsRefreshing = true; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var walks = await api.GetWalksAsync(List);
            Items.Clear();
            foreach (var w in walks) Items.Add(new WalkItem(w));
            IsEmpty = Items.Count == 0;
            OnPropertyChanged(nameof(EmptyMessage));
        }
        catch (ApiException ex) { await Ui.ShowAsync(ex); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private void Show(string list)
    {
        if (List == list) return;
        List = list;
        Reload();
    }

    [RelayCommand]
    private Task OpenAsync(WalkItem item) => Routes.OpenWalkAsync(item.Walk.Id);

    [RelayCommand]
    private Task NewAsync() => Shell.Current.GoToAsync(Routes.WalkEditor);
}

public partial class WalkViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(When), nameof(Facts), nameof(TagHint), nameof(Light), nameof(CanLeave), nameof(CanCancel),
        nameof(Location), nameof(Reason), nameof(PhotosTitle))]
    public partial WalkDto? Walk { get; set; }

    public ObservableCollection<UserSummaryDto> Attendees { get; } = [];
    public ObservableCollection<PhotoSummaryDto> Photos { get; } = [];

    public string Title => Walk?.Walk.Title ?? "Photo walk";
    public string When => Walk?.Walk is { } w ? $"{w.StartsAt.ToLocalTime():dddd d MMMM, HH:mm} – {w.EndsAt.ToLocalTime():HH:mm}" : "";
    public string Facts => Walk?.Walk is { } w
        ? $"📍 {w.MeetingPoint}\n👥 {w.AttendeeCount} going{(w.MaxPeople is { } m ? $" (up to {m})" : "")} · organised by {w.Organizer.DisplayName}"
        : "";
    public string TagHint => Walk is null ? "" : $"Tag your photos #{Walk.Walk.Tag} to add them here.";

    /// <summary>Golden and blue hours at the meeting point on the day, in this phone's time (SunTimes, worked out here).</summary>
    public string Light
    {
        get
        {
            if (Walk?.Walk is not { } w) return "";
            var day = SunTimes.For(SunTimes.LocalDate(w.StartsAt, w.Longitude), w.Latitude, w.Longitude);
            if (day.Sunrise is null) return "The sun doesn't rise or set here that day.";
            static string T(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm");
            static string R(TimeRange? r) => r is null ? "–" : $"{T(r.Start)}–{T(r.End)}";
            var overlaps = SunTimes.Overlaps(day, w.StartsAt, w.EndsAt);
            return $"🔵 Blue hour {R(day.MorningBlue)} · 🟠 Golden hour {R(day.MorningGolden)}\n" +
                   $"🌅 Sunrise {T(day.Sunrise.Value)} · Sunset {T(day.Sunset!.Value)}\n" +
                   $"🟠 Golden hour {R(day.EveningGolden)} · 🔵 Blue hour {R(day.EveningBlue)}" +
                   (overlaps.Count > 0 ? $"\n✨ The walk catches the {string.Join(" and ", overlaps)}." : "");
        }
    }

    public bool CanLeave => Walk?.Walk is { IsGoing: true, IsCancelled: false };
    public bool CanCancel => Walk is { CanEdit: true, Walk.IsCancelled: false };
    public string? Reason => Walk is { CanJoin: false, Walk.IsCancelled: false } ? Walk.CannotJoinReason : null;
    public string PhotosTitle => Walk is { PhotoCount: > 0 } w ? $"Photos from the walk ({w.PhotoCount})" : "Photos from the walk";
    public Location? Location => Walk?.Walk is { } w ? new Location(w.Latitude, w.Longitude) : null;

    /// <summary>Raised after loading, to move the map to the meeting point.</summary>
    public event EventHandler<Location>? Loaded;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid walkId) _id = walkId;
    }

    public Task LoadAsync() => _id == Guid.Empty ? Task.CompletedTask : RunAsync(async () =>
    {
        Walk = await api.GetWalkAsync(_id);
        Attendees.Clear();
        foreach (var a in Walk.Attendees) Attendees.Add(a);
        Photos.Clear();
        foreach (var p in Walk.Photos) Photos.Add(p);
        if (Location is { } at) Loaded?.Invoke(this, at);
    });

    [RelayCommand]
    private async Task JoinAsync()
    {
        await RunAsync(async () =>
        {
            await api.SetWalkAttendanceAsync(_id, true);
            await Ui.AlertAsync("You're going! You'll get a reminder the day before.");
        });
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        if (!await Ui.ConfirmAsync("Leave walk", "Not going any more?", "Leave")) return;
        await RunAsync(() => api.SetWalkAttendanceAsync(_id, false));
        await LoadAsync();
    }

    [RelayCommand]
    private Task EditAsync() => Shell.Current.GoToAsync(Routes.WalkEditor, new ShellNavigationQueryParameters { ["walk"] = Walk! });

    [RelayCommand]
    private async Task CancelWalkAsync()
    {
        if (!await Ui.ConfirmAsync("Cancel walk", "Cancel this walk? Everyone going will be told.", "Cancel walk")) return;
        await RunAsync(() => api.CancelWalkAsync(_id));
        await LoadAsync();
    }

    [RelayCommand]
    private Task DirectionsAsync() => Location is { } at
        ? Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(at, new MapLaunchOptions { Name = Walk!.Walk.MeetingPoint })
        : Task.CompletedTask;

    [RelayCommand]
    private Task OpenMemberAsync(UserSummaryDto member) => Routes.OpenMemberAsync(member.Id);

    [RelayCommand]
    private Task OpenPhotoAsync(PhotoSummaryDto photo) =>
        Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = photo.Id });
}

/// <summary>Organise or edit a walk. Times are the phone's local time; the meeting point is a map tap or one of your places.</summary>
public partial class WalkEditorViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid? _id;
    private List<PlaceDto> _places = [];

    public static readonly IReadOnlyList<int> DurationMinutes = [60, 90, 120, 180, 240, 300, 360, 480, 600, 720];
    public IReadOnlyList<string> DurationOptions { get; } =
        DurationMinutes.Select(m => m < 120 ? $"{m} minutes" : $"{m / 60.0:0.#} hours").ToList();

    [ObservableProperty] public partial string PageTitle { get; set; } = "Organise a walk";
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial DateTime Date { get; set; } = DateTime.Today.AddDays(7);
    [ObservableProperty] public partial TimeSpan Time { get; set; } = new(9, 0, 0);
    [ObservableProperty] public partial int DurationIndex { get; set; } = 2;
    [ObservableProperty] public partial string MeetingPoint { get; set; } = "";
    [ObservableProperty] public partial string MaxPeople { get; set; } = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlaces))]
    public partial IReadOnlyList<string> PlaceOptions { get; set; } = [];

    public bool HasPlaces => PlaceOptions.Count > 0;
    [ObservableProperty] public partial int PlaceIndex { get; set; } = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinText))]
    public partial Location? Pin { get; set; }

    public string PinText => Pin is null ? "Tap the map to put the pin where to meet." : "Pin set. Tap elsewhere on the map to move it.";
    public DateTime MinDate => DateTime.Today;

    /// <summary>Raised when the pin moves, so the page redraws it.</summary>
    public event EventHandler<Location>? PinMoved;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("walk", out var w) && w is WalkDto walk)
        {
            var s = walk.Walk;
            _id = s.Id;
            PageTitle = "Edit walk";
            (Title, Description, MeetingPoint) = (s.Title, walk.Description ?? "", s.MeetingPoint);
            var local = s.StartsAt.ToLocalTime();
            (Date, Time) = (local.Date, local.TimeOfDay);
            var minutes = (int)(s.EndsAt - s.StartsAt).TotalMinutes;
            DurationIndex = Math.Max(0, DurationMinutes.ToList().FindIndex(m => m >= minutes));
            MaxPeople = s.MaxPeople?.ToString() ?? "";
            SetPin(new Location(s.Latitude, s.Longitude));
        }
        _ = LoadPlacesAsync();
    }

    private async Task LoadPlacesAsync()
    {
        try
        {
            _places = (await api.GetPlacesAsync()).OrderBy(p => p.Name).ToList();
            PlaceOptions = _places.Select(p => p.Name).ToList();
        }
        catch (ApiException) { /* the map works without them */ }
    }

    partial void OnPlaceIndexChanged(int value)
    {
        if (value < 0 || value >= _places.Count) return;
        var place = _places[value];
        SetPin(new Location(place.Latitude, place.Longitude));
        if (string.IsNullOrWhiteSpace(MeetingPoint)) MeetingPoint = place.Name;
    }

    public void SetPin(Location at)
    {
        Pin = at;
        PinMoved?.Invoke(this, at);
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        int? max = null;
        if (!string.IsNullOrWhiteSpace(MaxPeople))
        {
            if (!int.TryParse(MaxPeople.Trim(), out var m) || m < 2 || m > WalkLimits.MaxPeopleLimit)
            {
                await Ui.AlertAsync("The group size is a number from 2 to 200, or empty for no limit.");
                return;
            }
            max = m;
        }
        if (Pin is null)
        {
            await Ui.AlertAsync("Tap the map (or pick one of your places) to set the meeting point.");
            return;
        }
        var start = new DateTimeOffset(Date.Date + Time, TimeZoneInfo.Local.GetUtcOffset(Date.Date + Time));
        var request = new SaveWalkRequest(Title.Trim(), Description.Trim(), start,
            start.AddMinutes(DurationMinutes[Math.Clamp(DurationIndex, 0, DurationMinutes.Count - 1)]), MeetingPoint.Trim(),
            Pin.Latitude, Pin.Longitude, max);
        var saved = _id is { } id ? await api.UpdateWalkAsync(id, request) : await api.CreateWalkAsync(request);
        await Shell.Current.GoToAsync("..");
        if (_id is null) await Routes.OpenWalkAsync(saved.Id);
    });
}
