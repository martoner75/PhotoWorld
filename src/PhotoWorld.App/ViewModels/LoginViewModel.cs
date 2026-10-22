using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PhotoWorld.App.Services;

namespace PhotoWorld.App.ViewModels;

public partial class LoginViewModel(AuthService auth, ILogger<LoginViewModel> logger) : ViewModelBase
{
    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <param name="provider">"Google", "Apple" or "LinkedIn" (<see cref="PhotoWorld.Shared.Auth.AuthProviders"/>).</param>
    [RelayCommand]
    private async Task SignInAsync(string provider)
    {
        if (IsBusy) return;
        Error = null;
        IsBusy = true;
        try
        {
            await auth.SignInAsync(provider);
            await Shell.Current.GoToAsync(Routes.Gallery);
        }
        catch (TaskCanceledException)
        {
            // The member closed the sign-in sheet.
        }
        catch (SignInException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sign-in failed");
            Error = "Sign-in failed. Check your connection and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task OpenPrivacyAsync() => Ui.OpenInBrowserAsync(AppConfig.PrivacyUrl);
}
