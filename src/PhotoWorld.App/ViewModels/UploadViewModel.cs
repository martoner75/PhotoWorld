using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.App.ViewModels;

/// <summary>One photo in a multi-photo upload: its own title, shown with a thumbnail and how it went.</summary>
public partial class UploadItem(FileResult file) : ObservableObject
{
    public FileResult File { get; } = file;
    public ImageSource Preview { get; } = ImageSource.FromFile(file.FullPath);

    [ObservableProperty] public partial string Title { get; set; } = Path.GetFileNameWithoutExtension(file.FileName).Replace('_', ' ').Trim();
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial bool Done { get; set; }
}

public partial class UploadViewModel(PhotoWorldApi api) : ViewModelBase
{
    /// <summary>Most photos one upload takes, on the website too.</summary>
    public const int MaxBatch = 20;

    private FileResult? _file;

    /// <summary>Several photos picked at once: each gets its own title; the other fields apply to all of them.</summary>
    public System.Collections.ObjectModel.ObservableCollection<UploadItem> Batch { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSingle))]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    public partial bool IsBatch { get; set; }

    public bool IsSingle => !IsBatch;

    [ObservableProperty]
    public partial string Progress { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto))]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    public partial ImageSource? Preview { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    public partial bool ShowOnHome { get; set; }

    /// <summary>Tags as typed ("fog wildlife"); #hashtags in the description are added too.</summary>
    [ObservableProperty]
    public partial string Tags { get; set; } = "";

    [ObservableProperty]
    public partial bool WantsFeedback { get; set; }

    /// <summary>"© name" drawn into the image at upload; it can't be taken off later.</summary>
    [ObservableProperty]
    public partial bool Watermark { get; set; }

    [ObservableProperty]
    public partial bool AllowDownload { get; set; } = true;

    // "Where was it taken?": one of the member's places (index 0 = none), to show the photo on the Spots map.
    private List<PlaceDto> _places = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> PlaceOptions { get; set; } = [NoPlace];

    [ObservableProperty]
    public partial int PlaceIndex { get; set; }

    public bool HasPlaces => _places.Count > 0;

    /// <summary>Clubs to share the upload to: the approved clubs the member is in.</summary>
    public System.Collections.ObjectModel.ObservableCollection<ClubShareItem> Clubs { get; } = [];
    public bool HasClubs => Clubs.Count > 0;

    private const string NoPlace = "Don't show it on the map";

    /// <summary>Called when the tab shows: places may have been added on the Places tab.</summary>
    public async Task LoadPlacesAsync()
    {
        try
        {
            var selected = PlaceIndex > 0 && PlaceIndex <= _places.Count ? _places[PlaceIndex - 1].Id : (Guid?)null;
            _places = (await api.GetPlacesAsync()).OrderBy(p => p.Name).ToList();
            PlaceOptions = [NoPlace, .. _places.Select(p => p.Name)];
            PlaceIndex = selected is { } id ? _places.FindIndex(p => p.Id == id) + 1 : 0;
            OnPropertyChanged(nameof(HasPlaces));
        }
        catch (ApiException)
        {
            // Without places the picker just isn't shown.
        }
        await LoadClubsAsync();
    }

    /// <summary>The member may have joined or left clubs since the tab last showed. Keeps what they ticked.</summary>
    private async Task LoadClubsAsync()
    {
        try
        {
            var ticked = Clubs.Where(c => c.Shared).Select(c => c.ClubId).ToHashSet();
            var clubs = (await api.GetMyClubsAsync())
                .Where(c => c.ReviewStatus == ClubReviewStatus.Approved && c.MyStatus == ClubMemberStatus.Active);
            Clubs.Clear();
            foreach (var c in clubs) Clubs.Add(new ClubShareItem(c.Id, c.Name, false) { Shared = ticked.Contains(c.Id) });
            OnPropertyChanged(nameof(HasClubs));
        }
        catch (ApiException)
        {
            // Without clubs the choice just isn't shown.
        }
    }

    /// <summary>Shares a new upload to the ticked clubs (each upload starts unshared).</summary>
    private Task ShareToClubsAsync(Guid photoId) =>
        ClubShareItem.ApplyAsync(api, photoId, Clubs.Select(c => new ClubShareItem(c.ClubId, c.Name, false) { Shared = c.Shared }));

    public bool HasPhoto => Preview is not null;

    public bool CanTakePhoto => MediaPicker.Default.IsCaptureSupported;

    public int TitleMaxLength => PhotoUploadLimits.MaxTitleLength;

    public int DescriptionMaxLength => PhotoUploadLimits.MaxDescriptionLength;

    [RelayCommand]
    private async Task PickAsync()
    {
        var files = (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = MaxBatch }))?.ToList() ?? [];
        if (files.Count <= 1)
        {
            Use(files.FirstOrDefault());
            return;
        }
        Reset();
        foreach (var file in files.Take(MaxBatch)) Batch.Add(new UploadItem(file));
        IsBatch = true;
    }

    [RelayCommand]
    private async Task TakeAsync() => Use(await MediaPicker.Default.CapturePhotoAsync());

    private void Use(FileResult? file)
    {
        if (file is null) return;
        Batch.Clear();
        IsBatch = false;
        _file = file;
        Preview = ImageSource.FromFile(file.FullPath);
    }

    private bool CanUpload() => IsBatch ? Batch.Count > 0 : _file is not null && !string.IsNullOrWhiteSpace(Title);

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private Task UploadAsync() => IsBatch ? UploadBatchAsync() : UploadOneAsync();

    /// <summary>
    /// Uploads the batch one photo at a time, so a failure only costs that photo; finished ones are kept off the list
    /// if it's tried again.
    /// </summary>
    private Task UploadBatchAsync() => RunAsync(async () =>
    {
        if (Batch.Any(i => !i.Done && string.IsNullOrWhiteSpace(i.Title)))
        {
            await Ui.AlertAsync("Give every photo a title first.");
            return;
        }
        var placeId = PlaceIndex > 0 && PlaceIndex <= _places.Count ? _places[PlaceIndex - 1].Id : (Guid?)null;
        var todo = Batch.Where(i => !i.Done).ToList();
        var failed = 0;
        var waiting = false;
        for (var n = 0; n < todo.Count; n++)
        {
            var item = todo[n];
            Progress = $"Uploading {n + 1} of {todo.Count}…";
            item.Status = "Uploading…";
            try
            {
                await using var stream = await item.File.OpenReadAsync();
                var photo = await api.UploadPhotoAsync(stream, item.File.FileName, ContentTypeOf(item.File), item.Title.Trim(),
                    Description.Trim(), ShowOnHome, placeId, Tags, WantsFeedback, Watermark, AllowDownload);
                waiting |= photo.ReviewStatus == PhotoReviewStatus.Pending;
                await ShareToClubsAsync(photo.Id);
                (item.Status, item.Done) = ("✓ Uploaded", true);
            }
            catch (ApiException ex)
            {
                item.Status = ex.Message;
                failed++;
            }
        }
        Progress = "";
        WeakReferenceMessenger.Default.Send(new PhotoUploadedMessage(Guid.Empty));
        if (failed == 0)
        {
            var count = todo.Count;
            Reset();
            await Ui.AlertAsync(waiting
                ? $"{count} photos shared. Thanks! They'll be in the gallery once an admin approves them; we'll let you know."
                : $"{count} photos shared. Thanks!");
            await Shell.Current.GoToAsync(Routes.Gallery);
        }
        else
        {
            await Ui.AlertAsync($"{todo.Count - failed} of {todo.Count} uploaded. The others say why on the list; fix them and tap Upload again.");
        }
    });

    private Task UploadOneAsync() => RunAsync(async () =>
    {
        await using var stream = await _file!.OpenReadAsync();
        if (stream.CanSeek && stream.Length > PhotoUploadLimits.MaxBytes)
        {
            await Ui.AlertAsync($"Photos can be up to {PhotoUploadLimits.MaxBytes / (1024 * 1024)} MB.");
            return;
        }

        var photo = await api.UploadPhotoAsync(stream, _file.FileName, ContentTypeOf(_file), Title.Trim(),
            Description.Trim(), ShowOnHome, PlaceIndex > 0 && PlaceIndex <= _places.Count ? _places[PlaceIndex - 1].Id : null, Tags, WantsFeedback,
            Watermark, AllowDownload);
        await ShareToClubsAsync(photo.Id);
        Reset();
        WeakReferenceMessenger.Default.Send(new PhotoUploadedMessage(photo.Id));
        await Shell.Current.GoToAsync($"{Routes.Gallery}/{Routes.Photo}", new ShellNavigationQueryParameters { ["id"] = photo.Id });
    });

    [RelayCommand]
    private void Reset()
    {
        _file = null;
        Preview = null;
        Batch.Clear();
        IsBatch = false;
        (Title, Description, ShowOnHome, PlaceIndex, Tags, WantsFeedback) = ("", "", false, 0, "", false);
        (Watermark, AllowDownload) = (false, true);
    }

    /// <summary>iOS often reports a UTI rather than a MIME type, so go by the file extension the picker gives.</summary>
    internal static string ContentTypeOf(FileResult file) => Path.GetExtension(file.FileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        ".heif" => "image/heif",
        _ => "image/jpeg",
    };
}

/// <summary>Lets the gallery know its first page is out of date.</summary>
public sealed record PhotoUploadedMessage(Guid PhotoId);
