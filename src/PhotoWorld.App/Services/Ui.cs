using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.Services;

/// <summary>Shell routes for pages pushed on top of the tabs.</summary>
public static class Routes
{
    public const string Login = "//login";
    public const string Gallery = "//main/gallery";
    public const string Photo = "photo";
    public const string Post = "post";
    public const string PostComments = "post-comments";
    public const string PlaceEditor = "place-editor";
}

/// <summary>Alerts and pickers shown over the current page.</summary>
public static class Ui
{
    public static Task AlertAsync(string message, string title = "PhotoWorld") =>
        Shell.Current.DisplayAlertAsync(title, message, "OK");

    public static Task<bool> ConfirmAsync(string title, string message, string accept) =>
        Shell.Current.DisplayAlertAsync(title, message, accept, "Cancel");

    /// <summary>Shows an API failure, except "signed out", which already sent the member to the sign-in page.</summary>
    public static Task ShowAsync(ApiException ex) =>
        ex.Status == System.Net.HttpStatusCode.Unauthorized ? Task.CompletedTask : AlertAsync(ex.Message);

    /// <summary>Asks why something is being reported. Returns the reason key, or null if cancelled.</summary>
    public static async Task<string?> PickReportReasonAsync()
    {
        var choice = await Shell.Current.DisplayActionSheetAsync("Why are you reporting this?", "Cancel", null,
            ReportRules.Reasons.Values.ToArray());
        return ReportRules.Reasons.FirstOrDefault(r => r.Value == choice).Key;
    }

    public static Task OpenInBrowserAsync(Uri uri) => Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
}
