using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Shared.Collections;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

/// <summary>Sent by the filter page (or "Same camera" on a photo) to change the gallery's camera filters.</summary>
public sealed record PhotoFilterChangedMessage(PhotoSearch Search);

public partial class GalleryViewModel : PagedListViewModel<PhotoSummaryDto>
{
    private readonly PhotoWorldApi _api;

    public GalleryViewModel(PhotoWorldApi api)
    {
        _api = api;
        // A new upload belongs at the top: start over next time the gallery shows.
        WeakReferenceMessenger.Default.Register<GalleryViewModel, PhotoUploadedMessage>(this, (vm, _) => vm.Items.Clear());
        WeakReferenceMessenger.Default.Register<GalleryViewModel, PhotoFilterChangedMessage>(this, (vm, m) =>
        {
            vm.Search = m.Search;
            vm.Items.Clear(); // reloads when the gallery shows again
        });
    }

    /// <summary>Only photos from members the viewer follows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyMessage))]
    public partial bool Following { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFiltered), nameof(FilterSummary), nameof(EmptyMessage))]
    public partial PhotoSearch Search { get; set; } = new();

    /// <summary>The challenge to show in the banner: the open one ending soonest, otherwise the next to start.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChallengeBanner))]
    public partial ChallengeSummaryDto? FeaturedChallenge { get; set; }

    /// <summary>Unread activity, shown on the bell.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActivityLabel))]
    public partial int UnreadActivity { get; set; }

    public string ActivityLabel => UnreadActivity switch { 0 => "🔔", > 99 => "🔔 99+", var n => $"🔔 {n}" };

    /// <summary>Called each time the gallery shows (coming back from Activity resets it).</summary>
    public async Task RefreshUnreadAsync()
    {
        try { UnreadActivity = (await _api.GetUnreadActivityAsync()).Count; }
        catch (ApiException) { /* the bell just keeps its last count */ }
    }

    [RelayCommand]
    private Task OpenActivityAsync() => Shell.Current.GoToAsync(Routes.Activity);

    public bool IsFiltered => !Search.IsEmpty;

    public string FilterSummary => "Camera filters on: " + string.Join(", ", Search.Describe());

    public string? ChallengeBanner => FeaturedChallenge is { } c
        ? $"🏆 Challenge: {c.Title} · {c.When(DateTimeOffset.UtcNow, TimeZoneInfo.Local)}"
        : null;

    public string EmptyMessage =>
        IsFiltered ? "No photos match those camera settings."
        : Following ? "No photos from members you follow yet. Tap a member's name to see their page and follow them."
        : "No photos yet. Be the first to share one from the Upload tab.";

    protected override Task<PagedResult<PhotoSummaryDto>> FetchAsync(int page)
    {
        if (page == 1) _ = LoadChallengeAsync();
        return _api.GetPhotosAsync(page, following: Following, search: Search);
    }

    private async Task LoadChallengeAsync()
    {
        try
        {
            var challenges = await _api.GetChallengesAsync();
            FeaturedChallenge = challenges.FirstOrDefault(c => c.Phase == ChallengePhase.Open)
                                ?? challenges.FirstOrDefault(c => c.Phase == ChallengePhase.Upcoming);
        }
        catch (ApiException)
        {
            // The banner is optional; the gallery works without it.
        }
    }

    [RelayCommand]
    private void ShowFollowing(string which)
    {
        var following = which == "following";
        if (following == Following) return;
        Following = following;
        IsRefreshing = true;
    }

    [RelayCommand]
    private Task OpenFiltersAsync() =>
        Shell.Current.GoToAsync(Routes.PhotoFilter, new ShellNavigationQueryParameters { ["search"] = Search });

    [RelayCommand]
    private void ClearFilters()
    {
        Search = new PhotoSearch();
        IsRefreshing = true;
    }

    [RelayCommand]
    private Task OpenChallengeAsync() =>
        FeaturedChallenge is { } c ? Routes.OpenChallengeAsync(c.Id) : Task.CompletedTask;

    [RelayCommand]
    private Task OpenChallengesAsync() => Shell.Current.GoToAsync(Routes.Challenges);

    [RelayCommand]
    private Task OpenSpotsAsync() => Shell.Current.GoToAsync(Routes.Spots);

    [RelayCommand]
    private Task OpenAsync(PhotoSummaryDto photo) =>
        Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = photo.Id });
}

/// <summary>Gallery camera filters. Number fields are text so they can be left empty.</summary>
public partial class PhotoFilterViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private const string Any = "Any";

    public ObservableCollection<string> Cameras { get; } = [Any];
    public ObservableCollection<string> Lenses { get; } = [Any];

    [ObservableProperty] public partial string Camera { get; set; } = Any;
    [ObservableProperty] public partial string Lens { get; set; } = Any;
    [ObservableProperty] public partial string FocalMin { get; set; } = "";
    [ObservableProperty] public partial string FocalMax { get; set; } = "";
    [ObservableProperty] public partial string ApertureMin { get; set; } = "";
    [ObservableProperty] public partial string ApertureMax { get; set; } = "";
    [ObservableProperty] public partial string IsoMin { get; set; } = "";
    [ObservableProperty] public partial string IsoMax { get; set; } = "";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("search", out var value) && value is PhotoSearch s)
        {
            SetChoice(Cameras, s.Camera, v => Camera = v);
            SetChoice(Lenses, s.Lens, v => Lens = v);
            (FocalMin, FocalMax) = (Text(s.FocalMin), Text(s.FocalMax));
            (ApertureMin, ApertureMax) = (Text(s.ApertureMin), Text(s.ApertureMax));
            (IsoMin, IsoMax) = (Text(s.IsoMin), Text(s.IsoMax));
        }
        _ = LoadOptionsAsync();
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            var options = await api.GetCameraOptionsAsync();
            foreach (var camera in options.Cameras.Where(c => !Cameras.Contains(c))) Cameras.Add(camera);
            foreach (var lens in options.Lenses.Where(l => !Lenses.Contains(l))) Lenses.Add(lens);
        }
        catch (ApiException)
        {
            // Without suggestions the pickers just offer "Any" and the current choice.
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        var search = new PhotoSearch
        {
            Camera = Camera == Any ? null : Camera,
            Lens = Lens == Any ? null : Lens,
        };
        var problems = new List<string>();
        search.FocalMin = Number(FocalMin, "focal length", problems);
        search.FocalMax = Number(FocalMax, "focal length", problems);
        search.ApertureMin = Number(ApertureMin, "aperture", problems);
        search.ApertureMax = Number(ApertureMax, "aperture", problems);
        search.IsoMin = (int?)Number(IsoMin, "ISO", problems);
        search.IsoMax = (int?)Number(IsoMax, "ISO", problems);
        if (problems.Count > 0)
        {
            await Ui.AlertAsync($"Check the {string.Join(" and ", problems.Distinct())}: use positive numbers, like 35 or 1.8.");
            return;
        }

        WeakReferenceMessenger.Default.Send(new PhotoFilterChangedMessage(search));
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void Clear()
    {
        (Camera, Lens) = (Any, Any);
        FocalMin = FocalMax = ApertureMin = ApertureMax = IsoMin = IsoMax = "";
    }

    private static void SetChoice(ObservableCollection<string> options, string? value, Action<string> set)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!options.Contains(value)) options.Add(value);
        set(value);
    }

    private static string Text(IFormattable? value) => value?.ToString(null, CultureInfo.CurrentCulture) ?? "";

    /// <summary>Accepts the device's decimal separator or a dot. Null when empty; a problem when not a positive number.</summary>
    private static decimal? Number(string text, string field, List<string> problems)
    {
        text = text.Trim();
        if (text.Length == 0) return null;
        if ((decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var n)
             || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out n)) && n > 0 && n < 10_000_000)
            return n;
        problems.Add(field);
        return null;
    }
}

public partial class PhotoDetailViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Byline), nameof(CameraSummary), nameof(IsUnderReview), nameof(AspectHeight),
        nameof(HasCamera), nameof(HasLens), nameof(PlaceLabel))]
    public partial PhotoDetailDto? Photo { get; set; }

    public bool HasCamera => Photo?.Camera is { } c && (c.Model ?? c.Make) is not null;
    public bool HasLens => Photo?.Camera.Lens is not null;

    [ObservableProperty]
    public partial bool LikedByMe { get; set; }

    [ObservableProperty]
    public partial int LikeCount { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommentCommand))]
    public partial string NewComment { get; set; } = "";

    public ObservableCollection<CommentDto> Comments { get; } = [];

    public int CommentMaxLength => PhotoUploadLimits.MaxCommentLength;

    public string Byline => Photo is null ? "" : $"by {Photo.Owner.DisplayName} · {Photo.CreatedAt.LocalDateTime:d MMM yyyy}";

    public bool IsUnderReview => Photo is { Moderation: not ModerationState.Visible };

    /// <summary>"Canon EOS R6 · 35 mm · f/1.8 · 1/250s · ISO 400", from whatever the photo recorded.</summary>
    public string? CameraSummary
    {
        get
        {
            if (Photo?.Camera is not { IsEmpty: false } c) return null;
            var camera = string.Join(' ', new[] { c.Make, c.Model }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
            string?[] parts =
            [
                camera, c.Lens,
                c.FocalLengthMm is { } f ? $"{f:0.#} mm" : null,
                c.Aperture is { } a ? $"f/{a:0.#}" : null,
                c.ExposureTime,
                c.Iso is { } iso ? $"ISO {iso}" : null,
                c.TakenAt is { } t ? $"taken {t.LocalDateTime:d MMM yyyy}" : null,
            ];
            return string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }

    /// <summary>Height that shows the whole photo at the screen's width (capped so tall shots don't fill the page).</summary>
    public double AspectHeight => Photo is { Width: > 0 } p
        ? Math.Min(DeviceDisplay.Current.MainDisplayInfo.Width / DeviceDisplay.Current.MainDisplayInfo.Density * p.Height / p.Width, 640)
        : 300;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid photoId) _ = LoadAsync(photoId);
    }

    private Task LoadAsync(Guid id) => RunAsync(async () =>
    {
        var photo = api.GetPhotoAsync(id);
        var comments = api.GetCommentsAsync(id);
        Photo = await photo;
        (LikedByMe, LikeCount) = (Photo.LikedByMe, Photo.LikeCount);
        Comments.Clear();
        foreach (var comment in await comments) Comments.Add(comment);
    });

    [RelayCommand]
    private async Task ToggleLikeAsync()
    {
        if (Photo is null) return;
        // Optimistic: flip now, settle with the server's count.
        var like = !LikedByMe;
        (LikedByMe, LikeCount) = (like, LikeCount + (like ? 1 : -1));
        try
        {
            var state = await api.SetLikeAsync(Photo.Id, like);
            (LikedByMe, LikeCount) = (state.LikedByMe, state.LikeCount);
        }
        catch (ApiException ex)
        {
            (LikedByMe, LikeCount) = (!like, LikeCount - (like ? 1 : -1));
            await Ui.ShowAsync(ex);
        }
    }

    private bool CanAddComment() => !string.IsNullOrWhiteSpace(NewComment);

    [RelayCommand(CanExecute = nameof(CanAddComment))]
    private Task AddCommentAsync() => RunAsync(async () =>
    {
        Comments.Add(await api.AddCommentAsync(Photo!.Id, NewComment.Trim()));
        NewComment = "";
    });

    [RelayCommand]
    private Task DeleteCommentAsync(CommentDto comment) => RunAsync(async () =>
    {
        if (!await Ui.ConfirmAsync("Delete comment", "Delete this comment?", "Delete")) return;
        await api.DeleteCommentAsync(Photo!.Id, comment.Id);
        Comments.Remove(comment);
    });

    public string? PlaceLabel => Photo?.Place is { } p ? $"📍 {p.Name}" : null;

    [RelayCommand]
    private Task OpenPlaceAsync() => Photo is null ? Task.CompletedTask
        : Shell.Current.GoToAsync(Routes.Spots, new ShellNavigationQueryParameters { ["photo"] = Photo.Id });

    /// <summary>
    /// Save to a collection: a sheet of the member's collections (✓ = already in it; choosing toggles), plus starting
    /// a new one.
    /// </summary>
    [RelayCommand]
    private Task SaveToCollectionAsync() => RunAsync(async () =>
    {
        if (Photo is null) return;
        const string newOne = "＋ New collection…";
        var choices = await api.GetCollectionChoicesAsync(Photo.Id);
        var labels = choices.Select(c => (c.ContainsPhoto ? "✓ " : "") + c.Title + (c.IsPublic ? "" : " (private)")).ToList();
        var picked = await Shell.Current.DisplayActionSheetAsync("Save to a collection", "Cancel", null, [.. labels, newOne]);
        if (picked == newOne)
        {
            var title = await Shell.Current.DisplayPromptAsync("New collection", "Name it, e.g. “Fog inspiration”. It starts private; " +
                "make it public on the website to show it on your page.", "Create", "Cancel", maxLength: CollectionLimits.TitleMax);
            if (string.IsNullOrWhiteSpace(title)) return;
            var created = await api.CreateCollectionAsync(new SaveCollectionRequest(title.Trim(), null, IsPublic: false));
            await api.SetCollectionPhotoAsync(created.Id, Photo.Id, true);
            await Ui.AlertAsync($"Saved to “{created.Title}”.");
            return;
        }
        var index = labels.IndexOf(picked ?? "");
        if (index < 0) return;
        var choice = choices[index];
        await api.SetCollectionPhotoAsync(choice.Id, Photo.Id, !choice.ContainsPhoto);
        await Ui.AlertAsync(choice.ContainsPhoto ? $"Removed from “{choice.Title}”." : $"Saved to “{choice.Title}”.");
    });

    [RelayCommand]
    private Task OpenOwnerAsync() => Photo is null ? Task.CompletedTask : Routes.OpenMemberAsync(Photo.Owner.Id);

    [RelayCommand]
    private Task SameCameraAsync() => ShowInGalleryAsync(new PhotoSearch { Camera = Photo?.Camera.Model ?? Photo?.Camera.Make });

    [RelayCommand]
    private Task SameLensAsync() => ShowInGalleryAsync(new PhotoSearch { Lens = Photo?.Camera.Lens });

    /// <summary>Back to the Gallery tab, filtered to photos like this one.</summary>
    private static async Task ShowInGalleryAsync(PhotoSearch search)
    {
        WeakReferenceMessenger.Default.Send(new PhotoFilterChangedMessage(search));
        await Shell.Current.GoToAsync(Routes.Gallery);
    }

    [RelayCommand]
    private Task ReportPhotoAsync() => ReportAsync(r => api.ReportPhotoAsync(Photo!.Id, null, r));

    [RelayCommand]
    private Task ReportCommentAsync(CommentDto comment) => ReportAsync(r => api.ReportPhotoAsync(Photo!.Id, comment.Id, r));
}
