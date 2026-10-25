using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

/// <summary>A challenge in the list, with its wording worked out once.</summary>
public sealed record ChallengeItem(ChallengeSummaryDto Challenge)
{
    public string Phase => ChallengeDisplay.PhaseLabel(Challenge.Phase);
    public bool IsOpen => Challenge.Phase == ChallengePhase.Open;
    public string When => Challenge.When(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
    public string Entries => Challenge.EntryCount == 1 ? "1 entry" : $"{Challenge.EntryCount} entries";
    public string? Winner => Challenge.Winner is { } w ? $"🏆 {w.DisplayName}" : null;
}

public partial class ChallengesViewModel(PhotoWorldApi api) : ViewModelBase
{
    public ObservableCollection<ChallengeItem> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public void EnsureLoaded()
    {
        if (Items.Count == 0 && !IsRefreshing) IsRefreshing = true;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var challenges = await api.GetChallengesAsync();
            Items.Clear();
            foreach (var c in challenges) Items.Add(new ChallengeItem(c));
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
    private Task OpenAsync(ChallengeItem item) => Routes.OpenChallengeAsync(item.Challenge.Id);
}

/// <summary>An entry tile: the photo, its votes and place.</summary>
public sealed record EntryItem(ChallengeEntryDto Entry, ChallengePhase Phase)
{
    public PhotoSummaryDto Photo => Entry.Photo;
    public string Votes => ChallengeDisplay.Votes(Entry.Votes);
    public bool IsHidden => Entry.Photo.Moderation != ModerationState.Visible;

    /// <summary>"🏆 Winner", "#2"… for the top three; hidden entries have no place.</summary>
    public string? Badge => IsHidden ? "Hidden" : Entry.Rank switch
    {
        1 when Phase == ChallengePhase.Closed => "🏆 Winner",
        >= 1 and <= 3 => $"#{Entry.Rank}",
        _ => null,
    };

    public bool CanWithdraw => Entry.IsMine && Phase == ChallengePhase.Open;
}

public partial class ChallengeViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(When), nameof(Dates), nameof(Rules), nameof(CanEnter), nameof(EnterHint))]
    public partial ChallengeDto? Challenge { get; set; }

    public ObservableCollection<EntryItem> Entries { get; } = [];

    public string Title => Challenge?.Challenge.Title ?? "";
    public string When => Challenge is null ? "" : Challenge.Challenge.When(DateTimeOffset.UtcNow, TimeZoneInfo.Local);

    public string Dates => Challenge?.Challenge is not { } c ? ""
        : $"{c.StartsAt.LocalDateTime:d MMM} – {c.EndsAt.LocalDateTime:d MMM yyyy, HH:mm}";

    public string Rules => Challenge?.Challenge.Phase switch
    {
        ChallengePhase.Upcoming => "Entries open when the challenge starts. Start shooting!",
        ChallengePhase.Open => "Votes are likes: open a photo and like the ones you think should win. Only likes from " +
                               "other members, given after a photo is entered and before the end, count.",
        ChallengePhase.Closed => "This challenge has ended. Places are by likes received during the challenge; on a tie, " +
                                 "the photo entered first ranks higher.",
        _ => "",
    };

    public bool CanEnter => Challenge is { Challenge.Phase: ChallengePhase.Open, CanEnter: true } c && c.MyEntryCount < ChallengeLimits.EntriesPerMember;

    public string? EnterHint => Challenge is { Challenge.Phase: ChallengePhase.Open, CanEnter: false } club
        ? $"This challenge is for members of {club.Challenge.ClubName}. Join the club to enter."
        : Challenge is { Challenge.Phase: ChallengePhase.Open } c
        ? $"You can enter up to {ChallengeLimits.EntriesPerMember} of your photos ({c.MyEntryCount} entered)."
        : null;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid challengeId) _id = challengeId;
    }

    /// <summary>Called each time the page shows, so new likes, entries and withdrawals are reflected.</summary>
    public Task LoadAsync() => _id == Guid.Empty ? Task.CompletedTask : RunAsync(async () =>
    {
        Challenge = await api.GetChallengeAsync(_id);
        Entries.Clear();
        foreach (var e in Challenge.Entries) Entries.Add(new EntryItem(e, Challenge.Challenge.Phase));
    });

    [RelayCommand]
    private Task EnterAsync() => Challenge is null ? Task.CompletedTask
        : Shell.Current.GoToAsync(Routes.EnterChallenge, new ShellNavigationQueryParameters
        {
            ["id"] = _id,
            ["title"] = Title,
            ["entered"] = Challenge.Entries.Where(e => e.IsMine).Select(e => e.Photo.Id).ToHashSet(),
        });

    [RelayCommand]
    private async Task WithdrawAsync(EntryItem item)
    {
        if (!await Ui.ConfirmAsync("Withdraw", $"Withdraw “{item.Photo.Title}”? Its votes so far will be lost.", "Withdraw")) return;
        await RunAsync(() => api.WithdrawChallengeEntryAsync(_id, item.Photo.Id));
        await LoadAsync();
    }

    [RelayCommand]
    private Task OpenPhotoAsync(EntryItem item) =>
        Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = item.Photo.Id });
}

/// <summary>One of the member's photos on the "enter a photo" page.</summary>
public sealed record EntryChoice(PhotoSummaryDto Photo, bool IsEntered)
{
    public bool IsHidden => Photo.Moderation != ModerationState.Visible;
    public bool CanEnter => !IsEntered && !IsHidden;
    public string Label => IsEntered ? "✓ Entered" : IsHidden ? "Hidden" : Photo.Title;
}

/// <summary>Pick one of your own photos to enter in a challenge.</summary>
public partial class EnterChallengeViewModel(PhotoWorldApi api, AuthService auth)
    : PagedListViewModel<EntryChoice>, IQueryAttributable
{
    private Guid _challengeId;
    private HashSet<Guid> _entered = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid challengeId) _challengeId = challengeId;
        if (query.TryGetValue("title", out var title) && title is string t) Title = $"Enter “{t}”";
        if (query.TryGetValue("entered", out var entered) && entered is HashSet<Guid> ids) _entered = ids;
    }

    protected override async Task<PagedResult<EntryChoice>> FetchAsync(int page)
    {
        var photos = await api.GetPhotosAsync(page, ownerId: auth.User?.Id);
        return new PagedResult<EntryChoice>(photos.Items.Select(p => new EntryChoice(p, _entered.Contains(p.Id))).ToList(),
            photos.Page, photos.PageSize, photos.TotalCount);
    }

    [RelayCommand]
    private Task ChooseAsync(EntryChoice choice) => RunAsync(async () =>
    {
        if (!choice.CanEnter) return;
        if (!await Ui.ConfirmAsync("Enter photo", $"Enter “{choice.Photo.Title}” in this challenge?", "Enter")) return;
        await api.EnterChallengeAsync(_challengeId, choice.Photo.Id);
        await Shell.Current.GoToAsync("..");
        await Ui.AlertAsync("Your photo is in! Votes are the likes it gets until the challenge ends.");
    });
}
