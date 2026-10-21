using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.App.ViewModels;

public partial class UploadViewModel(PhotoWorldApi api) : ViewModelBase
{
    private FileResult? _file;

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

    // "Where was it taken?": one of the member's places (index 0 = none), to show the photo on the Spots map.
    private List<PlaceDto> _places = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> PlaceOptions { get; set; } = [NoPlace];

    [ObservableProperty]
    public partial int PlaceIndex { get; set; }

    public bool HasPlaces => _places.Count > 0;

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
    }

    public bool HasPhoto => Preview is not null;

    public bool CanTakePhoto => MediaPicker.Default.IsCaptureSupported;

    public int TitleMaxLength => PhotoUploadLimits.MaxTitleLength;

    public int DescriptionMaxLength => PhotoUploadLimits.MaxDescriptionLength;

    [RelayCommand]
    private async Task PickAsync() => Use((await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault());

    [RelayCommand]
    private async Task TakeAsync() => Use(await MediaPicker.Default.CapturePhotoAsync());

    private void Use(FileResult? file)
    {
        if (file is null) return;
        _file = file;
        Preview = ImageSource.FromFile(file.FullPath);
    }

    private bool CanUpload() => _file is not null && !string.IsNullOrWhiteSpace(Title);

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private Task UploadAsync() => RunAsync(async () =>
    {
        await using var stream = await _file!.OpenReadAsync();
        if (stream.CanSeek && stream.Length > PhotoUploadLimits.MaxBytes)
        {
            await Ui.AlertAsync($"Photos can be up to {PhotoUploadLimits.MaxBytes / (1024 * 1024)} MB.");
            return;
        }

        var photo = await api.UploadPhotoAsync(stream, _file.FileName, ContentTypeOf(_file), Title.Trim(),
            Description.Trim(), ShowOnHome, PlaceIndex > 0 && PlaceIndex <= _places.Count ? _places[PlaceIndex - 1].Id : null, Tags, WantsFeedback);
        Reset();
        WeakReferenceMessenger.Default.Send(new PhotoUploadedMessage(photo.Id));
        await Shell.Current.GoToAsync($"{Routes.Gallery}/{Routes.Photo}", new ShellNavigationQueryParameters { ["id"] = photo.Id });
    });

    [RelayCommand]
    private void Reset()
    {
        _file = null;
        Preview = null;
        (Title, Description, ShowOnHome, PlaceIndex, Tags, WantsFeedback) = ("", "", false, 0, "", false);
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
