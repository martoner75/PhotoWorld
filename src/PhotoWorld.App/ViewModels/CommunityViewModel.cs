using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Shared.Walks;

namespace PhotoWorld.App.ViewModels;

/// <summary>
/// The Community tab: clubs, photo walks and challenges in one place (the website's Community menu), with what's
/// coming up for the member: their next walks and the challenges open now.
/// </summary>
public partial class CommunityViewModel(PhotoWorldApi api) : ViewModelBase
{
    private const int NextWalks = 3;

    public ObservableCollection<WalkItem> Walks { get; } = [];
    public ObservableCollection<ChallengeSummaryDto> Challenges { get; } = [];

    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool HasWalks { get; set; }
    [ObservableProperty] public partial bool HasChallenges { get; set; }

    public void Reload() { if (!IsRefreshing) IsRefreshing = true; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            // Walks the member organises or is going to, soonest first (club walks included: they're in "mine").
            var walks = (await api.GetWalksAsync(WalkLists.Mine))
                .Where(w => !w.IsCancelled && w.EndsAt > now).OrderBy(w => w.StartsAt).Take(NextWalks).ToList();
            Walks.Clear();
            foreach (var w in walks) Walks.Add(new WalkItem(w));
            HasWalks = Walks.Count > 0;

            var open = (await api.GetChallengesAsync()).Where(c => c.Phase == ChallengePhase.Open).ToList();
            Challenges.Clear();
            foreach (var c in open) Challenges.Add(c);
            HasChallenges = Challenges.Count > 0;
        }
        catch (ApiException ex) { await Ui.ShowAsync(ex); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private Task OpenClubsAsync() => Shell.Current.GoToAsync(Routes.Clubs);

    [RelayCommand]
    private Task OpenWalksAsync() => Shell.Current.GoToAsync(Routes.Walks);

    [RelayCommand]
    private Task OpenWorkshopsAsync() => Shell.Current.GoToAsync(Routes.Workshops);

    [RelayCommand]
    private Task OpenChallengesAsync() => Shell.Current.GoToAsync(Routes.Challenges);

    [RelayCommand]
    private Task OpenWalkAsync(WalkItem item) => Routes.OpenWalkAsync(item.Walk.Id);

    [RelayCommand]
    private Task OpenChallengeAsync(ChallengeSummaryDto challenge) => Routes.OpenChallengeAsync(challenge.Id);

    [RelayCommand]
    private Task CreateAsync() => Routes.CreateAsync();
}
