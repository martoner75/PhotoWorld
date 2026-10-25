using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.App.ViewModels;

public sealed record ClubItem(ClubSummaryDto Club)
{
    public string Facts => string.Join(" · ", new[]
    {
        Club.City is null ? null : $"📍 {Club.City}" + (Club.DistanceKm is { } km ? $" ({km:0} km)" : ""),
        $"{Club.MemberCount} {(Club.MemberCount == 1 ? "member" : "members")}",
        Club.JoinPolicy == ClubJoinPolicy.Open ? "anyone can join" : "ask to join",
    }.Where(s => s is not null));

    public string? Badge => Club.ReviewStatus switch
    {
        ClubReviewStatus.Pending => "Waiting for approval",
        ClubReviewStatus.Rejected => "Not approved",
        _ => Club.MyStatus == ClubMemberStatus.Pending ? "Requested"
            : Club.MyRole switch { ClubRoles.Owner => "Owner", ClubRoles.Admin => "Admin", ClubRoles.Member => "Member", _ => null },
    };
}

/// <summary>The club directory (search, or near the phone) and the member's own clubs.</summary>
public partial class ClubsViewModel(PhotoWorldApi api) : ViewModelBase
{
    public ObservableCollection<ClubItem> Items { get; } = [];

    [ObservableProperty] public partial bool Mine { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    /// <summary>Set by "Near me"; cleared by a new search.</summary>
    private Location? _near;

    public string EmptyMessage => Mine ? "You're not in any clubs yet. Find one in the directory, or start your own."
        : _near is not null ? $"No clubs within {ClubLimits.NearKm:0} km yet. Why not start one?"
        : "No clubs match that yet. Why not start one?";

    public void Reload() { if (!IsRefreshing) IsRefreshing = true; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var clubs = Mine ? await api.GetMyClubsAsync() : (await api.GetClubsAsync(SearchText, _near, 1)).Items;
            Items.Clear();
            foreach (var c in clubs) Items.Add(new ClubItem(c));
            IsEmpty = Items.Count == 0;
            OnPropertyChanged(nameof(EmptyMessage));
        }
        catch (ApiException ex) { await Ui.ShowAsync(ex); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private void Show(string list)
    {
        var mine = list == "mine";
        if (Mine == mine) return;
        Mine = mine;
        Reload();
    }

    [RelayCommand]
    private void Search()
    {
        _near = null;
        Reload();
    }

    /// <summary>Sorts by distance from the phone. The location is rounded before it's sent, and not kept.</summary>
    [RelayCommand]
    private async Task NearMeAsync()
    {
        try
        {
            if (await Permissions.RequestAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
            {
                await Ui.AlertAsync("Allow location for PhotoWorld in Settings to find clubs near you, or search by city.");
                return;
            }
            _near = await Geolocation.Default.GetLastKnownLocationAsync()
                    ?? await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(10)));
            if (_near is null)
            {
                await Ui.AlertAsync("Couldn't find where you are right now. Try searching by city.");
                return;
            }
            (Mine, SearchText) = (false, "");
            Reload();
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            await Ui.AlertAsync("Location isn't available on this device. Try searching by city.");
        }
    }

    [RelayCommand]
    private Task OpenAsync(ClubItem item) => Routes.OpenClubAsync(item.Club.Id);

    [RelayCommand]
    private Task NewAsync() => Shell.Current.GoToAsync(Routes.ClubEditor);
}

public sealed record ClubMemberItem(ClubMemberDto Member)
{
    public string Name => Member.User.DisplayName;
    public string? Badge => Member.Role switch { ClubRoles.Owner => "Owner", ClubRoles.Admin => "Admin", _ => null };
}

/// <summary>One club: joining and leaving, its members, and running it for its owner and admins.</summary>
public partial class ClubViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Facts), nameof(JoinText), nameof(IsRequested), nameof(Notice), nameof(RequestsTitle),
        nameof(MembersTitle), nameof(IsMember), nameof(HasRequests))]
    public partial ClubDto? Club { get; set; }

    public ObservableCollection<ClubMemberItem> Members { get; } = [];
    public ObservableCollection<ClubMemberItem> Requests { get; } = [];

    public string Title => Club?.Summary.Name ?? "Club";
    public string Facts => Club is { } c ? new ClubItem(c.Summary).Facts + $"\nRun by {c.Owner.DisplayName}" : "";
    public string JoinText => Club?.Summary.JoinPolicy == ClubJoinPolicy.Open ? "Join the club" : "Ask to join";
    public bool IsRequested => Club?.Summary.MyStatus == ClubMemberStatus.Pending;
    public bool IsMember => Club is { CanLeave: true } && !IsRequested;
    public bool HasRequests => Requests.Count > 0;
    public string RequestsTitle => $"Asking to join ({Requests.Count})";
    public string MembersTitle => $"Members ({Members.Count})";

    /// <summary>For the owner: why only they can see the club.</summary>
    public string? Notice => Club switch
    {
        { IsOwner: true, Summary.ReviewStatus: ClubReviewStatus.Pending } => "Waiting for an admin to approve your club. Until then only you can see it.",
        { IsOwner: true, Summary.ReviewStatus: ClubReviewStatus.Rejected } c =>
            "This club wasn't approved, so only you can see it." + (c.ReviewNote is { } n ? $" The admin said: “{n}”" : ""),
        { IsOwner: true, Moderation: ModerationState.PendingReview } => "Hidden while we review reports.",
        { IsOwner: true, Moderation: ModerationState.Removed } => "Removed by a moderator.",
        _ => null,
    };

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid clubId) _id = clubId;
    }

    public Task LoadAsync() => _id == Guid.Empty ? Task.CompletedTask : RunAsync(async () =>
    {
        var club = await api.GetClubAsync(_id);
        var members = await api.GetClubMembersAsync(_id, pending: false);
        var requests = club.CanManageMembers ? await api.GetClubMembersAsync(_id, pending: true) : [];
        Members.Clear();
        foreach (var m in members) Members.Add(new ClubMemberItem(m));
        Requests.Clear();
        foreach (var r in requests) Requests.Add(new ClubMemberItem(r));
        Club = club;
    });

    [RelayCommand]
    private async Task JoinAsync()
    {
        await RunAsync(async () =>
        {
            var club = await api.JoinClubAsync(_id);
            await Ui.AlertAsync(club.Summary.MyStatus == ClubMemberStatus.Pending
                ? "Request sent. You'll hear under Activity when the club lets you in."
                : "Welcome to the club!");
        });
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        var question = IsRequested ? "Withdraw your request to join?" : $"Leave {Title}?";
        if (!await Ui.ConfirmAsync(IsRequested ? "Withdraw request" : "Leave club", question, IsRequested ? "Withdraw" : "Leave")) return;
        await RunAsync(() => api.LeaveClubAsync(_id));
        await LoadAsync();
    }

    [RelayCommand]
    private Task EditAsync() => Shell.Current.GoToAsync(Routes.ClubEditor, new ShellNavigationQueryParameters { ["club"] = Club! });

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!await Ui.ConfirmAsync("Delete club", $"Delete {Title}? Its members are let go. This can't be undone.", "Delete")) return;
        await RunAsync(async () =>
        {
            await api.DeleteClubAsync(_id);
            await Shell.Current.GoToAsync("..");
        });
    }

    [RelayCommand]
    private async Task ApproveAsync(ClubMemberItem item)
    {
        await RunAsync(() => api.ApproveClubMemberAsync(_id, item.Member.User.Id));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeclineAsync(ClubMemberItem item)
    {
        await RunAsync(() => api.RemoveClubMemberAsync(_id, item.Member.User.Id));
        await LoadAsync();
    }

    /// <summary>Tapping a member: their page, or (for the club's owner and admins) what they can do about them.</summary>
    [RelayCommand]
    private async Task MemberAsync(ClubMemberItem item)
    {
        var m = item.Member;
        var canManage = Club is { } c && !m.IsMe && m.Role != ClubRoles.Owner
                        && (c.IsOwner || (c.CanManageMembers && m.Role == ClubRoles.Member));
        if (!canManage)
        {
            await Routes.OpenMemberAsync(m.User.Id);
            return;
        }

        const string profile = "View profile", admin = "Make admin", member = "Make a member again",
            handOver = "Hand the club over", remove = "Remove from club";
        var options = Club!.IsOwner
            ? new[] { profile, m.Role == ClubRoles.Admin ? member : admin, handOver }
            : new[] { profile };
        var choice = await Shell.Current.DisplayActionSheetAsync(m.User.DisplayName, "Cancel", remove, options);
        switch (choice)
        {
            case profile:
                await Routes.OpenMemberAsync(m.User.Id);
                return;
            case admin or member:
                await RunAsync(() => api.SetClubRoleAsync(_id, m.User.Id, choice == admin ? ClubRoles.Admin : ClubRoles.Member));
                break;
            case handOver:
                if (!await Ui.ConfirmAsync("Hand over", $"Make {m.User.DisplayName} the owner? You stay on as an admin.", "Hand over")) return;
                await RunAsync(() => api.SetClubRoleAsync(_id, m.User.Id, ClubRoles.Owner));
                break;
            case remove:
                if (!await Ui.ConfirmAsync("Remove member", $"Remove {m.User.DisplayName} from the club?", "Remove")) return;
                await RunAsync(() => api.RemoveClubMemberAsync(_id, m.User.Id));
                break;
            default:
                return;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private Task ReportAsync() => ReportAsync(r => api.ReportClubAsync(_id, r));
}

public partial class ClubEditorViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid? _id;
    private List<PlaceDto> _places = [];
    private (double Lat, double Lon)? _currentArea;

    [ObservableProperty] public partial string PageTitle { get; set; } = "Start a club";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string City { get; set; } = "";
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial bool AskToJoin { get; set; }

    /// <summary>"Not set", then (when editing a club that has one) "Keep the current area", then the member's places.</summary>
    [ObservableProperty] public partial List<string> AreaOptions { get; set; } = ["Not set"];
    [ObservableProperty] public partial int AreaIndex { get; set; }

    public bool IsNew => _id is null;
    public int NameMaxLength => ClubLimits.NameMax;
    public int CityMaxLength => ClubLimits.CityMax;
    public int DescriptionMaxLength => ClubLimits.DescriptionMax;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("club", out var value) && value is ClubDto club)
        {
            _id = club.Summary.Id;
            PageTitle = "Edit club";
            (Name, City, Description) = (club.Summary.Name, club.Summary.City ?? "", club.Description ?? "");
            AskToJoin = club.Summary.JoinPolicy == ClubJoinPolicy.Approval;
            if (club.Latitude is { } lat && club.Longitude is { } lon) _currentArea = (lat, lon);
            OnPropertyChanged(nameof(IsNew));
        }
        _ = LoadPlacesAsync();
    }

    private async Task LoadPlacesAsync()
    {
        try
        {
            _places = (await api.GetPlacesAsync()).OrderBy(p => p.Name).ToList();
        }
        catch (ApiException)
        {
            _places = [];
        }
        var options = new List<string> { "Not set" };
        if (_currentArea is not null) options.Add("Keep the current area");
        options.AddRange(_places.Select(p => $"Around {p.Name}"));
        AreaOptions = options;
        AreaIndex = _currentArea is null ? 0 : 1;
    }

    private (double? Lat, double? Lon) SelectedArea()
    {
        var offset = _currentArea is null ? 1 : 2;
        if (AreaIndex <= 0) return (null, null);
        if (_currentArea is { } current && AreaIndex == 1) return (current.Lat, current.Lon);
        var place = _places.ElementAtOrDefault(AreaIndex - offset);
        return place is null ? (null, null) : (place.Latitude, place.Longitude);
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var (lat, lon) = SelectedArea();
        var request = new SaveClubRequest(Name.Trim(), string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            string.IsNullOrWhiteSpace(City) ? null : City.Trim(), AskToJoin ? ClubJoinPolicy.Approval : ClubJoinPolicy.Open, lat, lon);
        if (_id is { } id)
        {
            await api.UpdateClubAsync(id, request);
            await Shell.Current.GoToAsync("..");
            return;
        }
        var club = await api.CreateClubAsync(request);
        if (club.Summary.ReviewStatus != ClubReviewStatus.Approved)
            await Ui.AlertAsync("Your club is waiting for an admin to approve it. You'll hear under Activity, usually within a day.");
        await Shell.Current.GoToAsync("..");
        await Routes.OpenClubAsync(club.Summary.Id);
    });
}
