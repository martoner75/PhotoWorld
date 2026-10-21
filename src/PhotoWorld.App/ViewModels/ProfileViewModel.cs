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
    [NotifyPropertyChangedFor(nameof(NotifyStatus), nameof(AwaitingConfirmation))]
    public partial EmailNotificationsDto? Notifications { get; set; }

    public bool AwaitingConfirmation => Notifications is { Enabled: true, Confirmed: false };

    public string NotifyStatus => Notifications switch
    {
        { Enabled: true, Confirmed: true } n => $"On: {NotificationFrequency.Labels[n.Frequency].ToLowerInvariant()}, to {n.Email}.",
        { Enabled: true, Confirmed: false } n => $"Waiting for you to confirm {n.Email}: open the link we emailed you.",
        _ => "Off.",
    };

    // ----- Activity: which kinds of notification the member wants -----
    [ObservableProperty] public partial bool ActivityLikes { get; set; } = true;
    [ObservableProperty] public partial bool ActivityComments { get; set; } = true;
    [ObservableProperty] public partial bool ActivityFollows { get; set; } = true;
    [ObservableProperty] public partial bool ActivityFollowedPosts { get; set; } = true;
    [ObservableProperty] public partial bool ActivityChallenges { get; set; } = true;

    private void ShowActivitySettings(ActivitySettingsDto s) =>
        (ActivityLikes, ActivityComments, ActivityFollows, ActivityFollowedPosts, ActivityChallenges) =
        (s.Likes, s.Comments, s.Follows, s.FollowedPosts, s.Challenges);

    [RelayCommand]
    private Task SaveActivitySettingsAsync() => RunAsync(async () =>
    {
        ShowActivitySettings(await api.SaveActivitySettingsAsync(new ActivitySettingsDto(
            ActivityLikes, ActivityComments, ActivityFollows, ActivityFollowedPosts, ActivityChallenges)));
        await Ui.AlertAsync("Activity settings saved.");
    });

    [RelayCommand]
    private Task OpenActivityAsync() => Shell.Current.GoToAsync(Routes.Activity);

    /// <summary>Members the signed-in member blocked, to unblock from here.</summary>
    public ObservableCollection<UserSummaryDto> Blocked { get; } = [];

    [ObservableProperty]
    public partial bool HasBlocked { get; set; }

    public async Task LoadAsync()
    {
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
        var saved = await api.SaveNotificationsAsync(new SaveEmailNotificationsRequest(NotifyEmail.Trim(), frequency, NotifyEnabled));
        ShowNotifications(saved);
        await Ui.AlertAsync(!saved.Enabled ? "Email notifications are off."
            : saved.Confirmed ? $"Saved. We'll email {saved.Email} about new blog posts."
            : $"Almost there: we've sent a confirmation link to {saved.Email}. Notifications start once you open it.");
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
    private Task OpenChallengesAsync() => Shell.Current.GoToAsync(Routes.Challenges);

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
        if (await Ui.ConfirmAsync("Sign out", "Sign out of PhotoWorld on this device?", "Sign out")) auth.SignOut();
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
