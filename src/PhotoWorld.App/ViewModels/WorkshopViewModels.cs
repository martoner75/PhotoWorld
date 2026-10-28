using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Workshops;

namespace PhotoWorld.App.ViewModels;

public sealed record WorkshopItem(WorkshopSummaryDto Workshop)
{
    public string When => Workshop.StartsAt.ToLocalTime().ToString("ddd d MMM, HH:mm");
    public string Format => $"Chat{(Workshop.HasVideoLink ? " and video call" : "")} · {(int)(Workshop.EndsAt - Workshop.StartsAt).TotalMinutes} min";
    public string Going => $"{Workshop.AttendeeCount}{(Workshop.MaxPeople is { } m ? $"/{m}" : "")} going · {Workshop.Host.DisplayName}";
    public string? ClubOnly => Workshop.ClubName is { } club ? $"{club} members only" : null;
    public string? Badge => Workshop.IsCancelled ? "Cancelled" : Workshop.IsHost ? "Hosting" : Workshop.IsGoing ? "Going" : null;
}

/// <summary>One chat message as the workshop page shows it.</summary>
public sealed record ChatMessageItem(WorkshopMessageDto Message)
{
    public string Author => Message.Author.DisplayName + (Message.IsHost ? " · host" : "");
    public string When => Message.CreatedAt.ToLocalTime().ToString("ddd HH:mm");
}

public partial class WorkshopsViewModel(PhotoWorldApi api) : ViewModelBase
{
    public ObservableCollection<WorkshopItem> Items { get; } = [];

    [ObservableProperty] public partial string List { get; set; } = WorkshopLists.Upcoming;
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    public string EmptyMessage => List switch
    {
        WorkshopLists.Mine => "You're not hosting or going to any workshops yet.",
        WorkshopLists.Past => "No past workshops yet.",
        _ => "No workshops coming up. Why not host the first one?",
    };

    public void Reload() { if (!IsRefreshing) IsRefreshing = true; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var workshops = await api.GetWorkshopsAsync(List);
            Items.Clear();
            foreach (var w in workshops) Items.Add(new WorkshopItem(w));
            IsEmpty = Items.Count == 0;
            OnPropertyChanged(nameof(EmptyMessage));
        }
        catch (ApiException ex) { await Ui.ShowAsync(ex); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private void Show(string list)
    {
        if (List == list) return;
        List = list;
        Reload();
    }

    [RelayCommand]
    private Task OpenAsync(WorkshopItem item) => Routes.OpenWorkshopAsync(item.Workshop.Id);

    [RelayCommand]
    private Task NewAsync() => Shell.Current.GoToAsync(Routes.WorkshopEditor);
}

/// <summary>
/// A workshop's page and its chat. While the page is on screen the chat asks for new messages every
/// <see cref="WorkshopLimits.ChatRefresh"/> (the page starts and stops it).
/// </summary>
public partial class WorkshopViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid _id;
    private long _lastId;
    private bool _polling;
    private IDispatcherTimer? _timer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(When), nameof(Format), nameof(Going), nameof(CanLeave), nameof(CanCancel),
        nameof(Reason), nameof(HasVideoLink), nameof(ChatHint), nameof(CanReadChat), nameof(CanChat))]
    public partial WorkshopDto? Workshop { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Draft { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; set; }

    public ObservableCollection<UserSummaryDto> Attendees { get; } = [];
    public ObservableCollection<ChatMessageItem> Messages { get; } = [];

    public string Title => Workshop?.Workshop.Title ?? "Online workshop";
    public string When => Workshop?.Workshop is { } w ? $"{w.StartsAt.ToLocalTime():dddd d MMMM, HH:mm} – {w.EndsAt.ToLocalTime():HH:mm}" : "";
    public string Format => Workshop?.Workshop is { } w
        ? (w.ClubName is { } club ? $"For members of {club} · " : "") + $"Chat on PhotoCommunity{(w.HasVideoLink ? ", plus a video call" : "")}"
        : "";
    public string Going => Workshop?.Workshop is { } w
        ? $"{w.AttendeeCount} going{(w.MaxPeople is { } m ? $" (up to {m})" : "")} · hosted by {w.Host.DisplayName}"
        : "";

    public bool CanLeave => Workshop?.Workshop is { IsGoing: true, IsCancelled: false };
    public bool CanCancel => Workshop is { CanEdit: true, Workshop.IsCancelled: false };
    public string? Reason => Workshop is { CanJoin: false, Workshop.IsCancelled: false } ? Workshop.CannotJoinReason : null;
    public bool HasVideoLink => Workshop?.VideoLink is not null && Workshop.Workshop.IsCancelled is false;
    public bool CanReadChat => Workshop?.CanReadChat == true;
    public bool CanChat => Workshop?.CanChat == true;

    /// <summary>Under the chat: why there's no box to write in, or an empty chat's invitation.</summary>
    public string? ChatHint => Workshop switch
    {
        null => null,
        { CanReadChat: false } => "Join the workshop to read and take part in its chat.",
        { CanChat: false } w => w.CannotChatReason,
        _ when Messages.Count == 0 => "No messages yet. Say hello and share what you'd like to cover.",
        _ => null,
    };

    /// <summary>Raised when messages were added at the end, so the page scrolls to the newest.</summary>
    public event EventHandler? MessagesAdded;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid workshopId) _id = workshopId;
    }

    public Task LoadAsync() => _id == Guid.Empty ? Task.CompletedTask : RunAsync(async () =>
    {
        Workshop = await api.GetWorkshopAsync(_id);
        Attendees.Clear();
        foreach (var a in Workshop.Attendees) Attendees.Add(a);
        Messages.Clear();
        _lastId = 0;
        if (Workshop.CanReadChat) Add(await api.GetWorkshopMessagesAsync(_id));
        OnPropertyChanged(nameof(ChatHint));
    });

    /// <summary>Starts asking for new messages (the page calls it when it appears).</summary>
    public void StartChat(IDispatcher dispatcher)
    {
        _timer ??= dispatcher.CreateTimer();
        _timer.Interval = WorkshopLimits.ChatRefresh;
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
        try { Add(await api.GetWorkshopMessagesAsync(_id, _lastId)); }
        catch (ApiException) { /* offline for a moment: the next tick tries again */ }
        finally { _polling = false; }
    }

    private void Add(IEnumerable<WorkshopMessageDto> messages)
    {
        var added = false;
        foreach (var m in messages.Where(m => m.Id > _lastId))
        {
            Messages.Add(new ChatMessageItem(m));
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
            var sent = await api.PostWorkshopMessageAsync(_id, body);
            Draft = "";
            await PollAsync(); // anything posted meanwhile comes first
            Add([sent]);
        }
        catch (ApiException ex) { await Ui.ShowAsync(ex); }
        finally { IsSending = false; }
    }

    [RelayCommand]
    private async Task MessageOptionsAsync(ChatMessageItem item)
    {
        if (!item.Message.CanDelete) return;
        if (!await Ui.ConfirmAsync("Delete message", "Delete this message from the chat?", "Delete")) return;
        try
        {
            await api.DeleteWorkshopMessageAsync(_id, item.Message.Id);
            Messages.Remove(item);
            OnPropertyChanged(nameof(ChatHint));
        }
        catch (ApiException ex) { await Ui.ShowAsync(ex); }
    }

    [RelayCommand]
    private async Task JoinAsync()
    {
        await RunAsync(async () =>
        {
            await api.SetWorkshopAttendanceAsync(_id, true);
            await Ui.AlertAsync("You're in! You'll get a reminder the day before, and you can say hello in the chat.");
        });
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        if (!await Ui.ConfirmAsync("Leave workshop", "Not going any more? You'll leave the chat.", "Leave")) return;
        await RunAsync(() => api.SetWorkshopAttendanceAsync(_id, false));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OpenVideoCallAsync()
    {
        if (Workshop?.VideoLink is not { } link || !Uri.TryCreate(link, UriKind.Absolute, out var uri)) return;
        // Opens the call's own app (Zoom, Meet, Teams) when it's installed, otherwise the browser.
        if (!await Launcher.Default.TryOpenAsync(uri)) await Browser.Default.OpenAsync(uri, BrowserLaunchMode.External);
    }

    [RelayCommand]
    private Task EditAsync() => Shell.Current.GoToAsync(Routes.WorkshopEditor, new ShellNavigationQueryParameters { ["workshop"] = Workshop! });

    [RelayCommand]
    private async Task CancelWorkshopAsync()
    {
        if (!await Ui.ConfirmAsync("Cancel workshop", "Cancel this workshop? Everyone going will be told, and the chat closes.", "Cancel workshop")) return;
        await RunAsync(() => api.CancelWorkshopAsync(_id));
        await LoadAsync();
    }

    [RelayCommand]
    private Task OpenMemberAsync(UserSummaryDto member) => Routes.OpenMemberAsync(member.Id);
}

/// <summary>Host or edit a workshop. Times are the phone's local time.</summary>
public partial class WorkshopEditorViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid? _id;

    public static readonly IReadOnlyList<int> DurationMinutes = [30, 45, 60, 90, 120, 180, 240, 360, 480];
    public IReadOnlyList<string> DurationOptions { get; } =
        DurationMinutes.Select(m => m < 120 ? $"{m} minutes" : $"{m / 60.0:0.#} hours").ToList();

    [ObservableProperty] public partial string PageTitle { get; set; } = "Host a workshop";
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial DateTime Date { get; set; } = DateTime.Today.AddDays(7);
    [ObservableProperty] public partial TimeSpan Time { get; set; } = new(19, 0, 0);
    [ObservableProperty] public partial int DurationIndex { get; set; } = 2;
    [ObservableProperty] public partial string VideoLink { get; set; } = "";
    [ObservableProperty] public partial string MaxPeople { get; set; } = "";

    /// <summary>New workshops only: "Everyone", then the approved clubs the member is in. Index 0 is a public workshop.</summary>
    private List<Shared.Clubs.ClubSummaryDto> _clubs = [];
    private Guid? _presetClub;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClubs))]
    public partial IReadOnlyList<string> ClubOptions { get; set; } = [];
    [ObservableProperty] public partial int ClubIndex { get; set; }
    public bool HasClubs => _id is null && ClubOptions.Count > 1;

    public DateTime MinDate => DateTime.Today;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("workshop", out var w) && w is WorkshopDto workshop)
        {
            var s = workshop.Workshop;
            _id = s.Id;
            PageTitle = "Edit workshop";
            (Title, Description, VideoLink) = (s.Title, workshop.Description ?? "", workshop.VideoLink ?? "");
            var local = s.StartsAt.ToLocalTime();
            (Date, Time) = (local.Date, local.TimeOfDay);
            var minutes = (int)(s.EndsAt - s.StartsAt).TotalMinutes;
            DurationIndex = Math.Max(0, DurationMinutes.ToList().FindIndex(m => m >= minutes));
            MaxPeople = s.MaxPeople?.ToString() ?? "";
        }
        else if (query.TryGetValue("clubId", out var c) && c is Guid clubId)
        {
            _presetClub = clubId;
        }
        if (_id is null) _ = LoadClubsAsync();
    }

    private async Task LoadClubsAsync()
    {
        try
        {
            _clubs = (await api.GetMyClubsAsync())
                .Where(c => c.ReviewStatus == Shared.Clubs.ClubReviewStatus.Approved && c.MyStatus == Shared.Clubs.ClubMemberStatus.Active)
                .ToList();
            ClubOptions = ["Everyone (public workshop)", .. _clubs.Select(c => $"Members of {c.Name}")];
            ClubIndex = _presetClub is { } preset ? _clubs.FindIndex(c => c.Id == preset) + 1 : 0;
        }
        catch (ApiException) { /* a public workshop, then */ }
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        int? max = null;
        if (!string.IsNullOrWhiteSpace(MaxPeople))
        {
            if (!int.TryParse(MaxPeople.Trim(), out var m) || m < 2 || m > WorkshopLimits.MaxPeopleLimit)
            {
                await Ui.AlertAsync("The group size is a number from 2 to 200, or empty for no limit.");
                return;
            }
            max = m;
        }
        var start = new DateTimeOffset(Date.Date + Time, TimeZoneInfo.Local.GetUtcOffset(Date.Date + Time));
        var request = new SaveWorkshopRequest(Title.Trim(), Description.Trim(), start,
            start.AddMinutes(DurationMinutes[Math.Clamp(DurationIndex, 0, DurationMinutes.Count - 1)]),
            string.IsNullOrWhiteSpace(VideoLink) ? null : VideoLink.Trim(), max,
            ClubIndex > 0 && ClubIndex <= _clubs.Count ? _clubs[ClubIndex - 1].Id : null);
        var saved = _id is { } id ? await api.UpdateWorkshopAsync(id, request) : await api.CreateWorkshopAsync(request);
        await Shell.Current.GoToAsync("..");
        if (_id is null) await Routes.OpenWorkshopAsync(saved.Id);
    });
}
