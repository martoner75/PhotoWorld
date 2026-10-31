using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Collabs;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.App.ViewModels;

public sealed record CollabItem(CollabSummaryDto Collab)
{
    public string When => Collab.StartsAt.ToLocalTime().ToString("ddd d MMM, HH:mm");
    public string Role => CollabRoles.Label(Collab.Role);
    public string Terms => CollabTerms.Label(Collab.Terms);
    public string Where => Collab.Area + (Collab.DistanceKm is { } km ? $" · {km:0.#} km" : "");
    public string Poster => $"{Collab.Poster.DisplayName} · {CollabText.Confirmed(Collab.PosterConfirmedCount)}";
    public bool HasCover => Collab.CoverUrl is not null;

    public string? Badge => Collab switch
    {
        { IsCancelled: true } => "Cancelled",
        { IsPoster: true, ReviewStatus: CollabReviewStatus.Pending } => "Waiting for approval",
        { IsPoster: true, ReviewStatus: CollabReviewStatus.Rejected } => "Not approved",
        { IsPoster: true } => "Your call",
        { MyReplyStatus: CollabReplyStatus.Accepted } => "You're in",
        { MyReplyStatus: CollabReplyStatus.Pending } => "Replied",
        { MyReplyStatus: CollabReplyStatus.Declined } => "Declined",
        _ => null,
    };
}

/// <summary>Wording shared by the Collabs screens.</summary>
public static class CollabText
{
    public static string Confirmed(int count) => count switch
    {
        0 => "new on Collabs",
        1 => "1 confirmed collab",
        _ => $"{count} confirmed collabs",
    };
}

/// <summary>A reply as the poster sees it on their call.</summary>
public sealed record CollabReplyItem(CollabReplyDto Reply)
{
    public string Name => Reply.Member.DisplayName;
    public string Details => CollabText.Confirmed(Reply.ConfirmedCount) + (Reply.PortfolioSlug is not null ? " · has a portfolio" : "");
    public string Status => Reply.Status switch
    {
        CollabReplyStatus.Accepted => Reply.PosterConfirmed
            ? Reply.MemberConfirmed ? "Accepted · confirmed by both of you" : "Accepted · you confirmed it"
            : "Accepted",
        CollabReplyStatus.Declined => "Declined",
        _ => "Waiting for you",
    };
    public bool IsAccepted => Reply.Status == CollabReplyStatus.Accepted;
}

/// <summary>One chat message on a call's page.</summary>
public sealed record CollabMessageItem(CollabMessageDto Message)
{
    public string Author => Message.Author.DisplayName + (Message.IsPoster ? " · poster" : "");
    public string When => Message.CreatedAt.ToLocalTime().ToString("ddd HH:mm");
}

/// <summary>
/// The Collabs board (open calls, near the phone once the member allows it, by role and terms) and the member's own
/// calls and replies.
/// </summary>
public partial class CollabsViewModel(PhotoWorldApi api) : PagedListViewModel<CollabItem>
{
    public static readonly double[] Distances = [10, 25, 50, 100, 150];

    public IReadOnlyList<string> RoleOptions { get; } = ["Anyone", .. CollabRoles.All.Select(CollabRoles.PluralLabel)];
    public IReadOnlyList<string> TermsOptions { get; } = ["Any terms", .. CollabTerms.All.Select(CollabTerms.Label)];
    public IReadOnlyList<string> DistanceOptions { get; } = Distances.Select(km => $"within {km:0} km").ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(EmptyMessage))]
    public partial bool Mine { get; set; }

    [ObservableProperty] public partial int RoleIndex { get; set; }
    [ObservableProperty] public partial int TermsIndex { get; set; }
    [ObservableProperty] public partial int DistanceIndex { get; set; } = 2; // 50 km

    /// <summary>Set by "Near me" (or straight away when location is already allowed); rounded before it's sent, never kept.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NearLabel), nameof(EmptyMessage), nameof(IsNear))]
    public partial Location? Near { get; set; }

    private bool _askedForLocation;

    public bool IsOpen => !Mine;
    public bool IsNear => Near is not null;
    public string NearLabel => Near is null ? "Near me" : "Near me ✓";

    public string EmptyMessage => Mine ? "You haven't posted or replied to any calls yet."
        : Near is not null ? $"No open calls {DistanceOptions[DistanceIndex]} yet. Try further away, or post the first one."
        : "No open calls match. Why not post the first one?";

    public void Reload() { if (!IsRefreshing) IsRefreshing = true; }

    /// <summary>On first view: if the member already allowed location, start near them.</summary>
    public async Task StartAsync()
    {
        if (!_askedForLocation)
        {
            _askedForLocation = true;
            try
            {
                if (await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() == PermissionStatus.Granted)
                    Near = await Geolocation.Default.GetLastKnownLocationAsync();
            }
            catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException) { }
        }
        Reload();
    }

    protected override async Task<PagedResult<CollabItem>> FetchAsync(int page)
    {
        var role = RoleIndex > 0 ? CollabRoles.All[RoleIndex - 1] : null;
        var terms = TermsIndex > 0 ? CollabTerms.All[TermsIndex - 1] : null;
        var result = await api.GetCollabsAsync(Mine ? CollabLists.Mine : CollabLists.Open, role, terms, Mine ? null : Near,
            Distances[Math.Clamp(DistanceIndex, 0, Distances.Length - 1)], page);
        OnPropertyChanged(nameof(EmptyMessage));
        return new PagedResult<CollabItem>(result.Items.Select(c => new CollabItem(c)).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    partial void OnRoleIndexChanged(int value) => Reload();
    partial void OnTermsIndexChanged(int value) => Reload();
    partial void OnDistanceIndexChanged(int value) { if (Near is not null) Reload(); }

    [RelayCommand]
    private void Show(string list)
    {
        var mine = list == CollabLists.Mine;
        if (Mine == mine) return;
        Mine = mine;
        Reload();
    }

    /// <summary>Toggles sorting by distance from the phone.</summary>
    [RelayCommand]
    private async Task NearMeAsync()
    {
        if (Near is not null)
        {
            Near = null;
            Reload();
            return;
        }
        try
        {
            if (await Permissions.RequestAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
            {
                await Ui.AlertAsync("Allow location for PhotoCommunity in Settings to see calls near you.");
                return;
            }
            Near = await Geolocation.Default.GetLastKnownLocationAsync()
                   ?? await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(10)));
            if (Near is null) await Ui.AlertAsync("Couldn't find where you are right now. Try again in a moment.");
            else Reload();
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            await Ui.AlertAsync("Location isn't available on this device.");
        }
    }

    [RelayCommand]
    private Task OpenAsync(CollabItem item) => Routes.OpenCollabAsync(item.Collab.Id);

    [RelayCommand]
    private Task NewAsync() => Shell.Current.GoToAsync(Routes.CollabEditor);
}

/// <summary>
/// A call's page: the call, the poster's replies or the member's own reply, the meeting point for the people going, and
/// the chat, which asks for new messages every <see cref="CollabLimits.ChatRefresh"/> while the page is on screen.
/// </summary>
public partial class CollabViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid _id;
    private long _lastId;
    private bool _polling;
    private IDispatcherTimer? _timer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(When), nameof(Role), nameof(Terms), nameof(Poster), nameof(Banner), nameof(Light),
        nameof(HasMeetingPoint), nameof(MeetingPoint), nameof(MeetingHint), nameof(IsPoster), nameof(ShowReplyForm), nameof(ReplyButton),
        nameof(MyReplyStatus), nameof(HasMyReply), nameof(CanWithdraw), nameof(WithdrawText), nameof(CanConfirmMine), nameof(CanCancel),
        nameof(CanReadChat), nameof(CanChat), nameof(ChatHint), nameof(CannotReplyReason), nameof(TagHint), nameof(HasPhotos),
        nameof(CanReport), nameof(RepliesHint), nameof(HasGoing))]
    public partial CollabDto? Collab { get; set; }

    [ObservableProperty] public partial string ReplyText { get; set; } = "";
    [ObservableProperty] public partial bool SharePortfolio { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Draft { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; set; }

    public ObservableCollection<PhotoSummaryDto> Photos { get; } = [];
    public ObservableCollection<CollabReplyItem> Replies { get; } = [];
    public ObservableCollection<UserSummaryDto> Going { get; } = [];
    public ObservableCollection<CollabMessageItem> Messages { get; } = [];

    private CollabSummaryDto? S => Collab?.Collab;

    public string Title => S?.Title ?? "Collab call";
    public string When => S is { } c ? $"{c.StartsAt.ToLocalTime():dddd d MMMM, HH:mm} – {c.EndsAt.ToLocalTime():HH:mm}" : "";
    public string Role => S is { } c ? $"Looking for: {CollabRoles.Label(c.Role)}" : "";
    public string Terms => S is { } c ? CollabTerms.Label(c.Terms) + (Collab!.TermsNote is { } note ? $" · {note}" : "") : "";
    public string Poster => S is { } c ? $"Posted by {c.Poster.DisplayName} · {CollabText.Confirmed(c.PosterConfirmedCount)}" : "";

    public string? Banner => S switch
    {
        { IsCancelled: true } => "Cancelled: this collab isn't happening.",
        { IsPoster: true, ReviewStatus: CollabReviewStatus.Pending } => "Waiting for approval: only you can see this call until an admin has had a look.",
        { IsPoster: true, ReviewStatus: CollabReviewStatus.Rejected } =>
            "Not approved: only you can see this call." + (Collab!.ReviewNote is { } note ? $" The reason: {note}" : ""),
        _ => null,
    };

    /// <summary>Golden and blue hours in the area on the day, in this phone's time (SunTimes, worked out here).</summary>
    public string Light
    {
        get
        {
            if (Collab is not { } d) return "";
            var day = SunTimes.For(SunTimes.LocalDate(d.Collab.StartsAt, d.AreaLongitude), d.AreaLatitude, d.AreaLongitude);
            return LightText.Describe(day, SunTimes.Overlaps(day, d.Collab.StartsAt, d.Collab.EndsAt));
        }
    }

    public bool HasPhotos => Photos.Count > 0;
    public bool HasGoing => Going.Count > 0;
    public bool HasMeetingPoint => Collab?.MeetingPoint is not null;
    public string MeetingPoint => Collab?.MeetingPoint?.Name ?? S?.Area ?? "";
    public string MeetingHint => HasMeetingPoint
        ? S!.IsPoster ? "Only you and the people you accept can see this." : "Only the people going can see this."
        : $"The exact meeting point goes only to the people {S?.Poster.DisplayName} accepts.";

    public bool IsPoster => S?.IsPoster == true;
    public string? RepliesHint => IsPoster && Replies.Count == 0
        ? "No replies yet. Members see your call once it's approved; the people who follow you hear about it." : null;

    public bool HasMyReply => Collab?.MyReply is not null;
    public string? MyReplyStatus => Collab?.MyReply is { } r ? r.Status switch
    {
        CollabReplyStatus.Accepted => $"You're in! {S!.Poster.DisplayName} accepted your reply." + (r.MemberConfirmed
            ? r.PosterConfirmed ? " You've both confirmed it." : " You confirmed it." : ""),
        CollabReplyStatus.Declined => $"{S!.Poster.DisplayName} went with someone else this time.",
        _ => $"Reply sent. {S!.Poster.DisplayName} hasn't answered yet.",
    } : null;
    public bool ShowReplyForm => Collab?.CanReply == true;
    public string ReplyButton => HasMyReply ? "Save reply" : "Send reply";
    public string? CannotReplyReason => Collab is { CanReply: false, MyReply: null } && !IsPoster ? Collab.CannotReplyReason : null;
    public bool CanWithdraw => HasMyReply && S?.IsCancelled == false;
    public string WithdrawText => Collab?.MyReply?.Status == CollabReplyStatus.Accepted ? "Pull out" : "Withdraw reply";
    public bool CanConfirmMine => !IsPoster && Collab?.CanConfirm == true;

    public bool CanCancel => Collab is { CanEdit: true, Collab.IsCancelled: false };
    public bool CanReport => S is { IsPoster: false };
    public bool CanReadChat => Collab?.CanReadChat == true;
    public bool CanChat => Collab?.CanChat == true;
    public string TagHint => Collab is null ? "" : $"Tag your photos from this collab #{Collab.Tag} to gather them together.";

    public string? ChatHint => Collab switch
    {
        null or { CanReadChat: false } => null,
        { CanChat: false } c => c.CannotChatReason,
        _ when Messages.Count == 0 => "No messages yet. Say hello and agree the details.",
        _ => null,
    };

    public event EventHandler? MessagesAdded;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid collabId) _id = collabId;
    }

    public Task LoadAsync() => _id == Guid.Empty ? Task.CompletedTask : RunAsync(async () =>
    {
        var collab = await api.GetCollabAsync(_id);
        Photos.Clear();
        foreach (var p in collab.Photos) Photos.Add(p);
        Replies.Clear();
        foreach (var r in collab.Replies) Replies.Add(new CollabReplyItem(r));
        Going.Clear();
        if (collab.Accepted.Count > 0)
        {
            Going.Add(collab.Collab.Poster);
            foreach (var a in collab.Accepted) Going.Add(a);
        }
        if (collab.MyReply is { } mine && string.IsNullOrEmpty(ReplyText)) (ReplyText, SharePortfolio) = (mine.Message, true);
        Messages.Clear();
        _lastId = 0;
        Collab = collab;
        if (collab.CanReadChat) Add(await api.GetCollabMessagesAsync(_id));
        OnPropertyChanged(nameof(ChatHint));
    });

    public void StartChat(IDispatcher dispatcher)
    {
        _timer ??= dispatcher.CreateTimer();
        _timer.Interval = CollabLimits.ChatRefresh;
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void StopChat() => _timer?.Stop();

    private async void OnTick(object? sender, EventArgs e) => await PollAsync();

    private async Task PollAsync()
    {
        if (_polling || IsBusy || !CanReadChat) return;
        _polling = true;
        try { Add(await api.GetCollabMessagesAsync(_id, _lastId)); }
        catch (ApiException) { /* offline for a moment: the next tick tries again */ }
        finally { _polling = false; }
    }

    private void Add(IEnumerable<CollabMessageDto> messages)
    {
        var added = false;
        foreach (var m in messages.Where(m => m.Id > _lastId))
        {
            Messages.Add(new CollabMessageItem(m));
            _lastId = m.Id;
            added = true;
        }
        if (!added) return;
        OnPropertyChanged(nameof(ChatHint));
        MessagesAdded?.Invoke(this, EventArgs.Empty);
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(Draft) && !IsSending;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var body = Draft.Trim();
        IsSending = true;
        try
        {
            var sent = await api.PostCollabMessageAsync(_id, body);
            Draft = "";
            await PollAsync();
            Add([sent]);
        }
        catch (ApiException ex) { await Ui.ShowAsync(ex); }
        finally { IsSending = false; }
    }

    [RelayCommand]
    private async Task MessageOptionsAsync(CollabMessageItem item)
    {
        if (!item.Message.CanDelete) return;
        if (!await Ui.ConfirmAsync("Delete message", "Delete this message from the chat?", "Delete")) return;
        try
        {
            await api.DeleteCollabMessageAsync(_id, item.Message.Id);
            Messages.Remove(item);
            OnPropertyChanged(nameof(ChatHint));
        }
        catch (ApiException ex) { await Ui.ShowAsync(ex); }
    }

    [RelayCommand]
    private async Task ReplyAsync()
    {
        if (string.IsNullOrWhiteSpace(ReplyText))
        {
            await Ui.AlertAsync("Write a few words about yourself first.");
            return;
        }
        var first = !HasMyReply;
        await RunAsync(async () =>
        {
            await api.ReplyToCollabAsync(_id, new ReplyToCollabRequest(ReplyText.Trim(), SharePortfolio));
            if (first) await Ui.AlertAsync("Reply sent. If you're accepted, you'll get the meeting point and a private chat.");
        });
        await LoadAsync();
    }

    [RelayCommand]
    private async Task WithdrawAsync()
    {
        var accepted = Collab?.MyReply?.Status == CollabReplyStatus.Accepted;
        if (!await Ui.ConfirmAsync(WithdrawText,
                accepted ? "Pull out of this collab? You'll lose the meeting point and the chat." : "Withdraw your reply?", WithdrawText)) return;
        await RunAsync(() => api.WithdrawCollabReplyAsync(_id));
        ReplyText = "";
        await LoadAsync();
    }

    /// <summary>The poster's choices for one reply: accept, decline or remove, confirm, see the member or their portfolio.</summary>
    [RelayCommand]
    private async Task ReplyOptionsAsync(CollabReplyItem item)
    {
        var r = item.Reply;
        const string accept = "Accept", decline = "Decline", remove = "Remove from the collab", confirm = "We shot together ✓",
            profile = "See their profile", portfolio = "See their portfolio";
        var choices = new List<string>();
        if (!S!.IsCancelled && r.Status != CollabReplyStatus.Accepted) choices.Add(accept);
        if (!S.IsCancelled && r.Status == CollabReplyStatus.Pending) choices.Add(decline);
        if (!S.IsCancelled && r.Status == CollabReplyStatus.Accepted) choices.Add(remove);
        if (r.Status == CollabReplyStatus.Accepted && !r.PosterConfirmed && Collab!.CanConfirm) choices.Add(confirm);
        choices.Add(profile);
        if (r.PortfolioSlug is not null) choices.Add(portfolio);

        var choice = await Shell.Current.DisplayActionSheetAsync(r.Member.DisplayName, "Cancel", null, choices.ToArray());
        switch (choice)
        {
            case accept:
                if (!await Ui.ConfirmAsync("Accept", $"Accept {r.Member.DisplayName}? They'll get the meeting point and join the chat.", "Accept")) return;
                await RunAsync(() => api.AcceptCollabReplyAsync(_id, r.Member.Id));
                break;
            case decline:
                await RunAsync(() => api.DeclineCollabReplyAsync(_id, r.Member.Id));
                break;
            case remove:
                if (!await Ui.ConfirmAsync("Remove", $"Take {r.Member.DisplayName} off this collab? They lose the meeting point and the chat.", "Remove")) return;
                await RunAsync(() => api.DeclineCollabReplyAsync(_id, r.Member.Id));
                break;
            case confirm:
                await RunAsync(() => api.ConfirmCollabAsync(_id, r.Member.Id));
                break;
            case profile:
                await Routes.OpenMemberAsync(r.Member.Id);
                return;
            case portfolio:
                await Ui.OpenInBrowserAsync(new Uri(AppConfig.WebBaseUrl, $"p/{r.PortfolioSlug}"));
                return;
            default:
                return;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ConfirmMineAsync()
    {
        if (Collab?.MyReply is not { } mine) return;
        await RunAsync(async () =>
        {
            await api.ConfirmCollabAsync(_id, mine.Member.Id);
            await Ui.AlertAsync("Thanks! Once you've both confirmed, it counts on both your profiles.");
        });
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DirectionsAsync()
    {
        if (Collab?.MeetingPoint is not { } point) return;
        // The phone's maps app routes from where the phone is; PhotoWorld only passes the meeting point.
        const string walk = "Walking", drive = "Driving", transit = "Public transport";
        var mode = await Shell.Current.DisplayActionSheetAsync("Directions to the meeting point", "Cancel", null, walk, drive, transit) switch
        {
            walk => NavigationMode.Walking,
            drive => NavigationMode.Driving,
            transit => NavigationMode.Transit,
            _ => (NavigationMode?)null,
        };
        if (mode is null) return;
        try
        {
            await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(new Location(point.Latitude, point.Longitude),
                new MapLaunchOptions { Name = point.Name, NavigationMode = mode.Value });
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or InvalidOperationException)
        {
            await Ui.AlertAsync("No maps app could open the directions on this device.");
        }
    }

    [RelayCommand]
    private Task EditAsync() => Shell.Current.GoToAsync(Routes.CollabEditor, new ShellNavigationQueryParameters { ["collab"] = Collab! });

    [RelayCommand]
    private async Task CancelCollabAsync()
    {
        if (!await Ui.ConfirmAsync("Cancel collab", "Cancel this collab? Everyone who replied will be told, and the chat closes.", "Cancel collab")) return;
        await RunAsync(() => api.CancelCollabAsync(_id));
        await LoadAsync();
    }

    [RelayCommand]
    private Task ReportAsync() => ReportAsync(r => api.ReportCollabAsync(_id, r));

    [RelayCommand]
    private Task OpenMemberAsync(UserSummaryDto member) => Routes.OpenMemberAsync(member.Id);

    [RelayCommand]
    private Task OpenPosterAsync() => S is { } c ? Routes.OpenMemberAsync(c.Poster.Id) : Task.CompletedTask;

    [RelayCommand]
    private Task OpenPhotoAsync(PhotoSummaryDto photo) =>
        Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = photo.Id });
}

/// <summary>A photo the poster can add to the mood board.</summary>
public partial class CollabPhotoPick(PhotoSummaryDto photo) : ObservableObject
{
    public PhotoSummaryDto Photo { get; } = photo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChosen), nameof(Badge))]
    public partial int? Position { get; set; }

    public bool IsChosen => Position is not null;
    public string Badge => Position is { } n ? n.ToString() : "";
}

/// <summary>Post or edit a call. Times are the phone's local time; the meeting point is one of the member's places.</summary>
public partial class CollabEditorViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid? _id;
    private CollabMeetingPointDto? _currentPoint;
    private List<Guid> _presetPhotos = [];
    private readonly List<CollabPhotoPick> _order = [];
    private List<PlaceDto> _places = [];

    public static readonly IReadOnlyList<int> DurationMinutes = [30, 60, 90, 120, 180, 240, 360, 480, 720];
    public IReadOnlyList<string> DurationOptions { get; } =
        DurationMinutes.Select(m => m < 120 ? $"{m} minutes" : $"{m / 60.0:0.#} hours").ToList();
    public IReadOnlyList<string> RoleOptions { get; } = CollabRoles.All.Select(CollabRoles.Label).ToList();
    public IReadOnlyList<string> TermsOptions { get; } = CollabTerms.All.Select(CollabTerms.Label).ToList();

    [ObservableProperty] public partial string PageTitle { get; set; } = "Post a call";
    [ObservableProperty] public partial string SaveText { get; set; } = "Send for review";
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial int RoleIndex { get; set; }
    [ObservableProperty] public partial int TermsIndex { get; set; } = 1; // trade for photos
    [ObservableProperty] public partial string TermsNote { get; set; } = "";
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial DateTime Date { get; set; } = DateTime.Today.AddDays(7);
    [ObservableProperty] public partial TimeSpan Time { get; set; } = new(17, 0, 0);
    [ObservableProperty] public partial int DurationIndex { get; set; } = 2;
    [ObservableProperty] public partial string Area { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlaces), nameof(NoPlaces))]
    public partial IReadOnlyList<string> PlaceOptions { get; set; } = [];
    [ObservableProperty] public partial int PlaceIndex { get; set; } = -1;
    [ObservableProperty] public partial bool PlacesLoaded { get; set; }

    public bool HasPlaces => PlaceOptions.Count > 0;
    public bool NoPlaces => PlacesLoaded && PlaceOptions.Count == 0;

    public ObservableCollection<CollabPhotoPick> Picks { get; } = [];
    public bool HasCandidates => Picks.Count > 0;

    public DateTime MinDate => DateTime.Today;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("collab", out var c) && c is CollabDto collab)
        {
            var s = collab.Collab;
            _id = s.Id;
            (PageTitle, SaveText) = ("Edit call", "Save changes");
            (Title, TermsNote, Description, Area) = (s.Title, collab.TermsNote ?? "", collab.Description ?? "", s.Area);
            RoleIndex = Math.Max(0, CollabRoles.All.ToList().IndexOf(s.Role));
            TermsIndex = Math.Max(0, CollabTerms.All.ToList().IndexOf(s.Terms));
            var local = s.StartsAt.ToLocalTime();
            (Date, Time) = (local.Date, local.TimeOfDay);
            var minutes = (int)(s.EndsAt - s.StartsAt).TotalMinutes;
            DurationIndex = Math.Max(0, DurationMinutes.ToList().FindIndex(m => m >= minutes));
            _currentPoint = collab.MeetingPoint;
            _presetPhotos = collab.Photos.Select(p => p.Id).ToList();
        }
    }

    private bool _photosLoaded;

    /// <summary>
    /// Every time the page appears: the member's places (they may have just added one, keeping the one chosen), and the
    /// first time their photos.
    /// </summary>
    public async Task LoadChoicesAsync()
    {
        try
        {
            var chosen = PlaceIndex >= 0 && PlaceIndex < _places.Count ? _places[PlaceIndex].Id : (Guid?)null;
            _places = (await api.GetPlacesAsync()).OrderBy(p => p.Name).ToList();
            PlaceOptions = _places.Select(p => p.Name).ToList();
            PlaceIndex = chosen is { } keep ? _places.FindIndex(p => p.Id == keep)
                : _currentPoint is { } point ? _places.FindIndex(p => p.Latitude == point.Latitude && p.Longitude == point.Longitude)
                : _places.Count == 1 ? 0 : -1;

            if (_photosLoaded) return;
            _photosLoaded = true;
            var candidates = (await api.GetMyPortfolioAsync()).Candidates;
            Picks.Clear();
            _order.Clear();
            foreach (var photo in candidates) Picks.Add(new CollabPhotoPick(photo));
            foreach (var id in _presetPhotos)
                if (Picks.FirstOrDefault(p => p.Photo.Id == id) is { } pick) _order.Add(pick);
            Renumber();
            OnPropertyChanged(nameof(HasCandidates));
        }
        catch (ApiException ex) { await Ui.ShowAsync(ex); }
        finally { PlacesLoaded = true; }
    }

    [RelayCommand]
    private async Task TogglePickAsync(CollabPhotoPick pick)
    {
        if (pick.IsChosen) _order.Remove(pick);
        else if (_order.Count >= CollabLimits.MaxPhotos)
        {
            await Ui.AlertAsync($"Pick up to {CollabLimits.MaxPhotos} mood photos.");
            return;
        }
        else _order.Add(pick);
        Renumber();
    }

    private void Renumber()
    {
        foreach (var p in Picks) p.Position = null;
        for (var i = 0; i < _order.Count; i++) _order[i].Position = i + 1;
    }

    [RelayCommand]
    private Task AddPlaceAsync() => Shell.Current.GoToAsync(Routes.PlaceEditor);

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (PlaceIndex < 0 || PlaceIndex >= _places.Count)
        {
            await Ui.AlertAsync("Pick one of your places as the meeting point.");
            return;
        }
        var start = new DateTimeOffset(Date.Date + Time, TimeZoneInfo.Local.GetUtcOffset(Date.Date + Time));
        var request = new SaveCollabRequest(Title.Trim(), CollabRoles.All[Math.Clamp(RoleIndex, 0, CollabRoles.All.Count - 1)],
            CollabTerms.All[Math.Clamp(TermsIndex, 0, CollabTerms.All.Count - 1)],
            string.IsNullOrWhiteSpace(TermsNote) ? null : TermsNote.Trim(),
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            start, start.AddMinutes(DurationMinutes[Math.Clamp(DurationIndex, 0, DurationMinutes.Count - 1)]),
            Area.Trim(), _places[PlaceIndex].Id, _order.Select(p => p.Photo.Id).ToList());
        var saved = _id is { } id ? await api.UpdateCollabAsync(id, request) : await api.CreateCollabAsync(request);
        await Shell.Current.GoToAsync("..");
        if (_id is null)
        {
            if (saved.ReviewStatus == CollabReviewStatus.Pending)
                await Ui.AlertAsync("Thanks! Your call goes on the board once an admin has had a look. We'll let you know.");
            await Routes.OpenCollabAsync(saved.Id);
        }
    });
}
