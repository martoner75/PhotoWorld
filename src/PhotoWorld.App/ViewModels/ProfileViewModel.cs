using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Notifications;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.App.ViewModels;

public partial class ProfileViewModel(AuthService auth, PhotoWorldApi api) : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemberSince))]
    public partial UserProfileDto? User { get; set; }

    public string MemberSince => User is null ? "" : $"Member since {User.MemberSince.LocalDateTime:MMMM yyyy}";

    // ----- Email notifications about new blog posts -----

    /// <summary>Labels for the picker, in the order of <see cref="NotificationFrequency.Labels"/>.</summary>
    public IReadOnlyList<string> FrequencyOptions { get; } = NotificationFrequency.Labels.Values.ToList();

    [ObservableProperty]
    public partial bool NotifyEnabled { get; set; }

    [ObservableProperty]
    public partial string NotifyEmail { get; set; } = "";

    [ObservableProperty]
    public partial int FrequencyIndex { get; set; } = 1; // daily

    [ObservableProperty]
    public partial bool WeeklySummary { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotifyStatus), nameof(AwaitingConfirmation))]
    public partial EmailNotificationsDto? Notifications { get; set; }


    public bool AwaitingConfirmation => Notifications is { Confirmed: false } n && (n.Enabled || n.WeeklySummary);

    public string NotifyStatus => Notifications switch
    {
        { Confirmed: true } n when n.Enabled || n.WeeklySummary => $"On: {string.Join(" and ", new[]
            {
                n.Enabled ? $"new blog posts {NotificationFrequency.Labels[n.Frequency].ToLowerInvariant()}" : null,
                n.WeeklySummary ? "your weekly summary" : null,
            }.OfType<string>())}, to {n.Email}.",
        { Confirmed: false } n when n.Enabled || n.WeeklySummary => $"Waiting for you to confirm {n.Email}: open the link we emailed you.",
        _ => "Off.",
    };

    // ----- Appearance: Automatic (follow the device), Light or Dark, remembered on this device -----
    [ObservableProperty]
    public partial string ThemeChoice { get; set; } = ChoiceName(ThemePalette.Saved);

    private static string ChoiceName(AppTheme theme) => theme switch { AppTheme.Light => "light", AppTheme.Dark => "dark", _ => "auto" };

    [RelayCommand]
    private void SetTheme(string choice) =>
        ThemePalette.Choose(choice switch { "light" => AppTheme.Light, "dark" => AppTheme.Dark, _ => AppTheme.Unspecified });

    // Also follows the top-bar icon. This view model is a singleton (Me and Settings share it), so no unsubscribing.
    private bool _followsThemeChoice;

    // ----- Activity: which kinds of notification the member wants, in the sections the website uses too -----

    /// <summary>
    /// <see cref="ActivityText.SettingGroups"/> as switches. Push isn't listed: it's kept as the member set it on the
    /// website, until the app can receive push itself.
    /// </summary>
    public ObservableCollection<ActivitySettingGroup> ActivityGroups { get; } = [];

    /// <summary>The settings as loaded, so saving keeps anything not shown here (push).</summary>
    private ActivitySettingsDto _activitySettings = ActivitySettingsDto.AllOn;

    private void ShowActivitySettings(ActivitySettingsDto s)
    {
        _activitySettings = s;
        ActivityGroups.Clear();
        foreach (var (heading, names) in ActivityText.SettingGroups)
        {
            var items = names.Where(n => n != nameof(ActivitySettingsDto.Push))
                .Select(n => new ActivitySettingItem(n, ActivityText.SettingLabels[n], s.Get(n))).ToList();
            if (items.Count > 0) ActivityGroups.Add(new ActivitySettingGroup(heading, items));
        }
    }

    [RelayCommand]
    private Task SaveActivitySettingsAsync() => RunAsync(async () =>
    {
        var settings = ActivityGroups.SelectMany(g => g.Items).Aggregate(_activitySettings, (s, i) => s.With(i.Name, i.IsOn));
        ShowActivitySettings(await api.SaveActivitySettingsAsync(settings));
        await Ui.AlertAsync("Activity settings saved.");
    });

    [RelayCommand]
    private Task OpenSettingsAsync() => Shell.Current.GoToAsync(Routes.Settings);

    [RelayCommand]
    private Task OpenClubsAsync() => Shell.Current.GoToAsync(Routes.Clubs, new ShellNavigationQueryParameters { ["mine"] = true });

    [RelayCommand]
    private Task OpenActivityAsync() => Shell.Current.GoToAsync(Routes.Activity);

    /// <summary>Members the signed-in member blocked, to unblock from here.</summary>
    public ObservableCollection<UserSummaryDto> Blocked { get; } = [];

    [ObservableProperty]
    public partial bool HasBlocked { get; set; }

    public async Task LoadAsync()
    {
        if (!_followsThemeChoice)
        {
            ThemePalette.Chosen += (_, theme) => ThemeChoice = ChoiceName(theme);
            _followsThemeChoice = true;
        }
        User ??= auth.User;
        try
        {
            User = await api.GetMeAsync();
            ShowNotifications(await api.GetNotificationsAsync());
            await LoadBlockedAsync();
            ShowActivitySettings(await api.GetActivitySettingsAsync());
        }
        catch (ApiException)
        {
            // Keep showing what the session knows; signing out is still possible.
        }
    }

    private void ShowNotifications(EmailNotificationsDto dto)
    {
        Notifications = dto;
        NotifyEnabled = dto.Enabled;
        NotifyEmail = dto.Email ?? dto.AccountEmail ?? "";
        FrequencyIndex = Math.Max(0, NotificationFrequency.Labels.Keys.ToList().IndexOf(dto.Frequency));
        WeeklySummary = dto.WeeklySummary;
    }

    [RelayCommand]
    private Task SaveNotificationsAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(NotifyEmail))
        {
            await Ui.AlertAsync("Enter the email address to send notifications to.");
            return;
        }
        var frequency = NotificationFrequency.Labels.Keys.ElementAt(Math.Clamp(FrequencyIndex, 0, NotificationFrequency.Labels.Count - 1));
        var saved = await api.SaveNotificationsAsync(new SaveEmailNotificationsRequest(NotifyEmail.Trim(), frequency, NotifyEnabled, WeeklySummary));
        ShowNotifications(saved);
        await Ui.AlertAsync(!saved.Enabled && !saved.WeeklySummary ? "Emails are off."
            : saved.Confirmed ? $"Saved. We'll email {saved.Email}."
            : $"Almost there: we've sent a confirmation link to {saved.Email}. Emails start once you open it.");
    });

    [RelayCommand]
    private Task ResendConfirmationAsync() => RunAsync(async () =>
    {
        await api.ResendNotificationConfirmationAsync();
        await Ui.AlertAsync("We've sent the confirmation link again.");
    });

    /// <summary>The member page others see: followers, challenge wins and photos.</summary>
    [RelayCommand]
    private Task OpenMyPageAsync() => User is null ? Task.CompletedTask : Routes.OpenMemberAsync(User.Id);

    [RelayCommand]
    private Task OpenCollectionsAsync() => Shell.Current.GoToAsync(Routes.Collections);

    [RelayCommand]
    private Task OpenInsightsAsync() => Shell.Current.GoToAsync(Routes.Insights);

    [RelayCommand]
    private Task OpenPortfolioAsync() => Shell.Current.GoToAsync(Routes.Portfolio);

    private async Task LoadBlockedAsync()
    {
        Blocked.Clear();
        foreach (var member in await api.GetBlockedMembersAsync()) Blocked.Add(member);
        HasBlocked = Blocked.Count > 0;
    }

    [RelayCommand]
    private Task UnblockAsync(UserSummaryDto member) => RunAsync(async () =>
    {
        if (!await Ui.ConfirmAsync("Unblock", $"Unblock {member.DisplayName}? You'll see their photos, posts and comments again.", "Unblock")) return;
        await api.SetBlockAsync(member.Id, block: false);
        await LoadBlockedAsync();
    });

    [RelayCommand]
    private Task OpenTermsAsync() => Ui.OpenInBrowserAsync(AppConfig.TermsUrl);

    [RelayCommand]
    private Task OpenPrivacyAsync() => Ui.OpenInBrowserAsync(AppConfig.PrivacyUrl);

    [RelayCommand]
    private Task OpenWebsiteAsync() => Ui.OpenInBrowserAsync(AppConfig.WebBaseUrl);

    [RelayCommand]
    private async Task SignOutAsync()
    {
        if (await Ui.ConfirmAsync("Sign out", "Sign out of PhotoCommunity on this device?", "Sign out")) auth.SignOut();
    }

    /// <summary>Same rule as the website: type DELETE to confirm. Required in-app for App Store apps with accounts.</summary>
    [RelayCommand]
    private Task DeleteAccountAsync() => RunAsync(async () =>
    {
        var typed = await Shell.Current.DisplayPromptAsync("Delete account",
            "This permanently deletes your account, photos, blog, places, likes and comments. It can't be undone.\n\nType DELETE to confirm.",
            accept: "Delete", cancel: "Cancel", placeholder: "DELETE", keyboard: Keyboard.Text);
        if (typed is null) return;
        if (typed.Trim() != "DELETE")
        {
            await Ui.AlertAsync("Type DELETE in capitals to confirm.");
            return;
        }
        await api.DeleteAccountAsync();
        auth.SignOut();
        await Ui.AlertAsync("Your account and everything you shared have been deleted.");
    });
}

/// <summary>One section of activity switches ("Community": challenges, walks, clubs…).</summary>
public sealed record ActivitySettingGroup(string Heading, IReadOnlyList<ActivitySettingItem> Items);

/// <summary>One activity switch, by its <see cref="ActivitySettingsDto"/> name.</summary>
public partial class ActivitySettingItem(string name, string label, bool isOn) : ObservableObject
{
    public string Name { get; } = name;
    public string Label { get; } = label;
    [ObservableProperty] public partial bool IsOn { get; set; } = isOn;
}
