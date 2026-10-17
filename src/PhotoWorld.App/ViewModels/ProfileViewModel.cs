using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.App.ViewModels;

public partial class ProfileViewModel(AuthService auth, PhotoWorldApi api) : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemberSince))]
    public partial UserProfileDto? User { get; set; }

    public string MemberSince => User is null ? "" : $"Member since {User.MemberSince.LocalDateTime:MMMM yyyy}";

    public async Task LoadAsync()
    {
        User ??= auth.User;
        try
        {
            User = await api.GetMeAsync();
        }
        catch (ApiException)
        {
            // Keep showing what the session knows; signing out is still possible.
        }
    }

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
