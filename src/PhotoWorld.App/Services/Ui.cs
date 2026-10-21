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
    public const string Member = "member";
    public const string PhotoFilter = "photo-filter";
    public const string Challenges = "challenges";
    public const string Challenge = "challenge";
    public const string EnterChallenge = "challenge-enter";
    public const string Activity = "activity";
    public const string Spots = "spots";
    public const string Collections = "collections";
    public const string Collection = "collection";
    public const string Explore = "explore";
    public const string Feedback = "feedback";
    public const string Walks = "walks";
    public const string Walk = "walk";
    public const string WalkEditor = "walk-editor";

    public static Task OpenWalkAsync(Guid id) =>
        Shell.Current.GoToAsync(Walk, new ShellNavigationQueryParameters { ["id"] = id });

    public static Task OpenMemberAsync(Guid id) =>
        Shell.Current.GoToAsync(Member, new ShellNavigationQueryParameters { ["id"] = id });

    public static Task OpenChallengeAsync(Guid id) =>
        Shell.Current.GoToAsync(Challenge, new ShellNavigationQueryParameters { ["id"] = id });
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
