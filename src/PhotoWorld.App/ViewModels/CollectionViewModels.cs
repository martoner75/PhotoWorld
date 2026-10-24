using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Collections;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

public sealed record CollectionItem(CollectionSummaryDto Collection)
{
    public string Facts => $"{Collection.PhotoCount} {(Collection.PhotoCount == 1 ? "photo" : "photos")}"
                           + (Collection.IsMine && !Collection.IsPublic ? " · private" : "");
}

/// <summary>The member's own collections, or another member's public ones (query "ownerId").</summary>
public partial class CollectionsViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid? _ownerId;

    public ObservableCollection<CollectionItem> Items { get; } = [];

    [ObservableProperty] public partial string Title { get; set; } = "My collections";
    [ObservableProperty] public partial bool IsEmpty { get; set; }
    [ObservableProperty] public partial bool IsRefreshing { get; set; }

    public bool IsMine => _ownerId is null;

    public string EmptyMessage => _ownerId is null
        ? "No collections yet. Tap New, or use ＋ Save on any photo to start one."
        : "No public collections yet.";

    [RelayCommand]
    private Task NewAsync() => Shell.Current.GoToAsync(Routes.CollectionEditor);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("ownerId", out var id) && id is Guid ownerId) _ownerId = ownerId;
        if (query.TryGetValue("name", out var name) && name is string n) Title = $"{n}'s collections";
        OnPropertyChanged(nameof(EmptyMessage));
    }

    public void EnsureLoaded()
    {
        if (!IsRefreshing) IsRefreshing = true;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var collections = await api.GetCollectionsAsync(_ownerId);
            Items.Clear();
            foreach (var c in collections) Items.Add(new CollectionItem(c));
            IsEmpty = Items.Count == 0;
        }
        catch (ApiException ex)
        {
            await Ui.ShowAsync(ex);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task OpenAsync(CollectionItem item) =>
        Shell.Current.GoToAsync(Routes.Collection, new ShellNavigationQueryParameters { ["id"] = item.Collection.Id });
}

/// <summary>The photos in one collection.</summary>
public partial class CollectionViewModel(PhotoWorldApi api) : PagedListViewModel<PhotoSummaryDto>, IQueryAttributable
{
    private Guid _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Byline), nameof(CanEdit))]
    public partial CollectionSummaryDto? Collection { get; set; }

    public string Byline => Collection is null ? "" : $"by {Collection.Owner.DisplayName} · {Collection.PhotoCount} photos"
                                                      + (Collection.IsPublic ? "" : " · private");
    public bool CanEdit => Collection?.IsMine == true;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid collectionId) _id = collectionId;
    }

    protected override async Task<PagedResult<PhotoSummaryDto>> FetchAsync(int page)
    {
        var dto = await api.GetCollectionAsync(_id, page);
        Collection = dto.Collection;
        return dto.Photos;
    }

    [RelayCommand]
    private Task OpenAsync(PhotoSummaryDto photo) =>
        Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = photo.Id });

    [RelayCommand]
    private Task RemoveAsync(PhotoSummaryDto photo) => RunAsync(async () =>
    {
        if (!await Ui.ConfirmAsync("Remove", $"Remove “{photo.Title}” from this collection?", "Remove")) return;
        await api.SetCollectionPhotoAsync(_id, photo.Id, false);
        Items.Remove(photo);
        IsEmpty = Items.Count == 0;
    });

    [RelayCommand]
    private Task EditAsync() => Shell.Current.GoToAsync(Routes.CollectionEditor, new ShellNavigationQueryParameters { ["id"] = _id });

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async () =>
    {
        if (Collection is null || !await Ui.ConfirmAsync("Delete collection",
                $"Delete “{Collection.Title}”? The photos themselves aren't touched.", "Delete")) return;
        await api.DeleteCollectionAsync(_id);
        await Shell.Current.GoToAsync("..");
    });
}
