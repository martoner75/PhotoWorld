using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

public sealed record MemberItem(MemberResultDto Result)
{
    public string Name => Result.Member.DisplayName;
    public string? AvatarUrl => Result.Member.AvatarUrl;
    public string Initial => Name.Length > 0 ? Name[..1] : "";
    public string Facts => $"{Result.PhotoCount} {(Result.PhotoCount == 1 ? "photo" : "photos")} · {Result.FollowerCount} "
                           + (Result.FollowerCount == 1 ? "follower" : "followers") + (Result.FollowedByMe ? " · following" : "");
}

/// <summary>
/// Explore (trending tags, photographers to follow, popular photos) and search (members, tags, photos, posts): the
/// same screen, switching to results while there's a search.
/// </summary>
public partial class ExploreViewModel(PhotoWorldApi api) : ViewModelBase
{
    public ObservableCollection<TagCountDto> Tags { get; } = [];
    public ObservableCollection<MemberItem> Members { get; } = [];
    public ObservableCollection<BlogPostSummaryDto> Posts { get; } = [];
    public ObservableCollection<PhotoSummaryDto> Photos { get; } = [];

    [ObservableProperty] public partial string Query { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhotoOfTheDay))]
    public partial PhotoOfTheDayDto? PhotoOfTheDay { get; set; }

    public bool HasPhotoOfTheDay => PhotoOfTheDay is not null && !IsSearch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagsTitle), nameof(MembersTitle), nameof(PhotosTitle), nameof(CanSeeAllPhotos), nameof(HasPhotoOfTheDay))]
    public partial bool IsSearch { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PhotosTitle), nameof(CanSeeAllPhotos))]
    public partial int PhotoCount { get; set; }

    [ObservableProperty] public partial bool HasTags { get; set; }
    [ObservableProperty] public partial bool HasMembers { get; set; }
    [ObservableProperty] public partial bool HasPosts { get; set; }
    [ObservableProperty] public partial bool NothingFound { get; set; }

    public string TagsTitle => IsSearch ? "Tags" : "Trending tags";
    public string MembersTitle => IsSearch ? "Members" : "Photographers to follow";
    public string PhotosTitle => IsSearch ? $"Photos ({PhotoCount})" : "Popular photos";
    public bool CanSeeAllPhotos => IsSearch && PhotoCount > Photos.Count;

    private bool _loaded;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;
        await LoadExploreAsync();
    }

    private Task LoadExploreAsync() => RunAsync(async () =>
    {
        var explore = await api.ExploreAsync();
        try { PhotoOfTheDay ??= await api.GetPhotoOfTheDayAsync(); } catch (ApiException) { /* optional */ }
        IsSearch = false;
        Show(explore.TrendingTags, explore.SuggestedMembers, [], explore.PopularPhotos, explore.PopularPhotos.Count);
    });

    [RelayCommand]
    private async Task SearchAsync()
    {
        var query = Query.Trim();
        if (query.Length < 2)
        {
            await LoadExploreAsync();
            return;
        }
        if (query.StartsWith('#') && !query.Contains(' ') && PhotoTags.Normalize(query) is { } tag)
        {
            await OpenTagAsync(new TagCountDto(tag, 0));
            return;
        }
        await RunAsync(async () =>
        {
            var results = await api.SearchAsync(query);
            IsSearch = true;
            Show(results.Tags, results.Members, results.Posts, results.Photos, results.PhotoCount);
            NothingFound = results.Members.Count == 0 && results.Tags.Count == 0 && results.PhotoCount == 0 && results.Posts.Count == 0;
        });
    }

    /// <summary>Cleared search box: back to Explore.</summary>
    partial void OnQueryChanged(string value)
    {
        if (IsSearch && string.IsNullOrWhiteSpace(value)) _ = LoadExploreAsync();
    }

    private void Show(IEnumerable<TagCountDto> tags, IEnumerable<MemberResultDto> members, IEnumerable<BlogPostSummaryDto> posts,
        IEnumerable<PhotoSummaryDto> photos, int photoCount)
    {
        Replace(Tags, tags);
        Replace(Members, members.Select(m => new MemberItem(m)));
        Replace(Posts, posts);
        Replace(Photos, photos);
        PhotoCount = photoCount;
        (HasTags, HasMembers, HasPosts, NothingFound) = (Tags.Count > 0, Members.Count > 0, Posts.Count > 0, false);
        OnPropertyChanged(nameof(CanSeeAllPhotos));
    }

    private static void Replace<T>(ObservableCollection<T> list, IEnumerable<T> items)
    {
        list.Clear();
        foreach (var item in items) list.Add(item);
    }

    /// <summary>Opens the Gallery tab filtered to a tag (or, for "See all", to the search words).</summary>
    [RelayCommand]
    private Task OpenTagAsync(TagCountDto tag) => ShowInGalleryAsync(new PhotoSearch { Tag = tag.Name });

    [RelayCommand]
    private Task SeeAllPhotosAsync() => ShowInGalleryAsync(new PhotoSearch { Text = Query.Trim() });

    private static async Task ShowInGalleryAsync(PhotoSearch search)
    {
        WeakReferenceMessenger.Default.Send(new PhotoFilterChangedMessage(search));
        await Shell.Current.GoToAsync(Routes.Gallery);
    }

    [RelayCommand]
    private Task OpenMemberAsync(MemberItem member) => Routes.OpenMemberAsync(member.Result.Member.Id);

    [RelayCommand]
    private Task OpenPostAsync(BlogPostSummaryDto post) =>
        Shell.Current.GoToAsync(Routes.Post, new ShellNavigationQueryParameters { ["id"] = post.Id });

    [RelayCommand]
    private Task OpenPhotoOfTheDayAsync() => PhotoOfTheDay is { } p ? OpenPhotoAsync(p.Photo) : Task.CompletedTask;

    [RelayCommand]
    private Task OpenPhotoAsync(PhotoSummaryDto photo) =>
        Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = photo.Id });
}
