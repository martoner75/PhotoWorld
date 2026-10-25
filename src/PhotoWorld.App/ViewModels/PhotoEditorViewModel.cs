using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.App.ViewModels;

/// <summary>A photo's details changed or it was deleted: open pages and lists showing it reload.</summary>
public sealed record PhotoChangedMessage(Guid PhotoId, bool Deleted = false);

/// <summary>The owner's editor for one photo (query "id"): details, the before version, and deleting it.</summary>
public partial class PhotoEditorViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid _id;
    private List<PlaceDto> _places = [];
    private const string NoPlace = "Don't show it on the map";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBefore), nameof(BeforeButton), nameof(BeforeHint), nameof(CanHaveBefore))]
    public partial PhotoDetailDto? Photo { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Title { get; set; } = "";

    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial string Tags { get; set; } = "";
    [ObservableProperty] public partial bool WantsFeedback { get; set; }
    [ObservableProperty] public partial bool AllowDownload { get; set; } = true;
    [ObservableProperty] public partial bool ShowOnHome { get; set; }
    [ObservableProperty] public partial IReadOnlyList<string> PlaceOptions { get; set; } = [NoPlace];
    [ObservableProperty] public partial int PlaceIndex { get; set; }

    public bool HasPlaces => _places.Count > 0;

    /// <summary>"Share to clubs": the approved clubs the owner is in. Changes are applied when they save.</summary>
    public System.Collections.ObjectModel.ObservableCollection<ClubShareItem> Clubs { get; } = [];
    public bool HasClubs => Clubs.Count > 0;

    public int TitleMaxLength => PhotoUploadLimits.MaxTitleLength;
    public int DescriptionMaxLength => PhotoUploadLimits.MaxDescriptionLength;

    // ----- Before & after -----
    public bool HasBefore => Photo?.BeforeImageUrl is not null;
    /// <summary>A refused photo can't get one (the website says the same).</summary>
    public bool CanHaveBefore => Photo is not null && Photo.ReviewStatus != PhotoReviewStatus.Rejected;
    public string BeforeButton => HasBefore ? "Replace before version" : "Add before version";
    public string BeforeHint => Photo?.ReviewStatus == PhotoReviewStatus.Approved
        ? "Add the unedited shot: viewers can switch between the two. As it's a new image, your photo goes back for " +
          "approval and is hidden from others until an admin approves it again."
        : "Add the unedited shot: viewers can switch between the two. Location and camera data are removed.";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid photoId)
        {
            _id = photoId;
            _ = LoadAsync();
        }
    }

    private Task LoadAsync() => RunAsync(async () =>
    {
        var placesTask = api.GetPlacesAsync();
        var p = await api.GetPhotoAsync(_id);
        Photo = p;
        (Title, Description, Tags) = (p.Title, p.Description ?? "", PhotoTags.Join(p.Tags ?? []));
        (WantsFeedback, AllowDownload, ShowOnHome) = (p.WantsFeedback, p.AllowDownload, p.ShowOnHome);
        try
        {
            _places = (await placesTask).OrderBy(x => x.Name).ToList();
        }
        catch (ApiException)
        {
            _places = []; // without places the picker just isn't shown
        }
        PlaceOptions = [NoPlace, .. _places.Select(x => x.Name)];
        PlaceIndex = p.Place is { } place ? _places.FindIndex(x => x.Id == place.Id) + 1 : 0;
        OnPropertyChanged(nameof(HasPlaces));

        Clubs.Clear();
        if (p.ReviewStatus != PhotoReviewStatus.Rejected)
        {
            try
            {
                foreach (var c in await api.GetPhotoClubsAsync(_id)) Clubs.Add(new ClubShareItem(c.ClubId, c.Name, c.Shared));
            }
            catch (ApiException)
            {
                // Without clubs the choice just isn't shown.
            }
        }
        OnPropertyChanged(nameof(HasClubs));
    });

    private bool CanSave() => Photo is not null && !string.IsNullOrWhiteSpace(Title);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var placeId = PlaceIndex > 0 && PlaceIndex <= _places.Count ? _places[PlaceIndex - 1].Id : (Guid?)null;
        await api.UpdatePhotoAsync(_id, new UpdatePhotoRequest(Title.Trim(), Description.Trim(), ShowOnHome, placeId,
            Tags, WantsFeedback, AllowDownload));
        if (await ClubShareItem.ApplyAsync(api, _id, Clubs) > 0)
            await Ui.AlertAsync("Saved, but the photo couldn't be shared to every club you picked. Try again in a moment.");
        WeakReferenceMessenger.Default.Send(new PhotoChangedMessage(_id));
        await Shell.Current.GoToAsync("..");
    });

    [RelayCommand]
    private Task PickBeforeAsync() => RunAsync(async () =>
    {
        var picked = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 });
        if (picked?.FirstOrDefault() is not { } file) return;
        var wasApproved = Photo?.ReviewStatus == PhotoReviewStatus.Approved;
        await using var stream = await file.OpenReadAsync();
        Photo = await api.AddBeforeAsync(_id, stream, file.FileName, UploadViewModel.ContentTypeOf(file));
        WeakReferenceMessenger.Default.Send(new PhotoChangedMessage(_id));
        await Ui.AlertAsync(wasApproved && Photo.ReviewStatus == PhotoReviewStatus.Pending
            ? "Before version added. As it's a new image, the photo is waiting for approval again; we'll let you know."
            : "Before version added. Open the photo and tap “Show before” to compare.");
    });

    [RelayCommand]
    private Task RemoveBeforeAsync() => RunAsync(async () =>
    {
        if (!await Ui.ConfirmAsync("Before version", "Remove the before version?", "Remove")) return;
        await api.RemoveBeforeAsync(_id);
        Photo = await api.GetPhotoAsync(_id);
        WeakReferenceMessenger.Default.Send(new PhotoChangedMessage(_id));
    });

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async () =>
    {
        if (!await Ui.ConfirmAsync("Delete photo",
                $"Delete “{Photo?.Title}”? Its likes, comments and feedback go too. This can't be undone.", "Delete")) return;
        await api.DeletePhotoAsync(_id);
        WeakReferenceMessenger.Default.Send(new PhotoChangedMessage(_id, Deleted: true));
        // Back past the photo's own page, which no longer exists.
        await Shell.Current.GoToAsync("../..");
    });
}
