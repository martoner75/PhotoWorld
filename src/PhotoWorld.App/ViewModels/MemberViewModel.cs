using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

/// <summary>A member's page: who they are, follow / unfollow, challenges won, and their photos.</summary>
public partial class MemberViewModel(PhotoWorldApi api) : PagedListViewModel<PhotoSummaryDto>, IQueryAttributable
{
    private Guid _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Facts), nameof(FollowCounts), nameof(CanFollow), nameof(HasWins), nameof(Wins), nameof(Initial))]
    public partial MemberProfileDto? Member { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FollowButtonText), nameof(FollowCounts))]
    public partial bool FollowedByMe { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FollowCounts))]
    public partial int FollowerCount { get; set; }

    public string Initial => Member is { DisplayName.Length: > 0 } m ? m.DisplayName[..1] : "";

    public string Facts => Member is not { } m ? ""
        : $"Member since {m.MemberSince.LocalDateTime:MMMM yyyy} · {m.PhotoCount} {(m.PhotoCount == 1 ? "photo" : "photos")}";

    public string FollowCounts => Member is not { } m ? ""
        : $"{FollowerCount} {(FollowerCount == 1 ? "follower" : "followers")} · following {m.FollowingCount}";

    public bool CanFollow => Member is { IsMe: false };
    public string FollowButtonText => FollowedByMe ? "Following ✓" : "Follow";

    public IReadOnlyList<ChallengeWinDto> Wins => Member?.Wins ?? [];
    public bool HasWins => Wins.Count > 0;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid memberId)
        {
            _id = memberId;
            _ = LoadMemberAsync();
        }
    }

    private Task LoadMemberAsync() => RunAsync(async () =>
    {
        Member = await api.GetMemberAsync(_id);
        (FollowedByMe, FollowerCount) = (Member.FollowedByMe, Member.FollowerCount);
    });

    protected override Task<PagedResult<PhotoSummaryDto>> FetchAsync(int page) => api.GetPhotosAsync(page, ownerId: _id);

    [RelayCommand]
    private async Task ToggleFollowAsync()
    {
        if (Member is null) return;
        if (FollowedByMe && !await Ui.ConfirmAsync("Unfollow", $"Stop following {Member.DisplayName}?", "Unfollow")) return;

        // Optimistic: flip now, settle with the server's count.
        var follow = !FollowedByMe;
        (FollowedByMe, FollowerCount) = (follow, FollowerCount + (follow ? 1 : -1));
        try
        {
            var state = await api.SetFollowAsync(Member.Id, follow);
            (FollowedByMe, FollowerCount) = (state.FollowedByMe, state.FollowerCount);
        }
        catch (ApiException ex)
        {
            (FollowedByMe, FollowerCount) = (!follow, FollowerCount - (follow ? 1 : -1));
            await Ui.ShowAsync(ex);
        }
    }

    [RelayCommand]
    private Task OpenWinAsync(ChallengeWinDto win) => Routes.OpenChallengeAsync(win.ChallengeId);

    [RelayCommand]
    private Task OpenAsync(PhotoSummaryDto photo) =>
        Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = photo.Id });
}
