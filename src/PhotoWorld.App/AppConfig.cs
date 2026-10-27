namespace PhotoWorld.App;

/// <summary>Where the app finds PhotoWorld.</summary>
public static class AppConfig
{
#if DEBUG
    // The iOS Simulator shares the Mac's network, so it reaches the API and website started with `dotnet run`.
    // The Android emulator does too after `adb reverse tcp:7041 tcp:7041` and `adb reverse tcp:7211 tcp:7211`
    // (localhost matters: it's the sign-in redirect registered with Google). Real phones can't: point these at a
    // deployed PhotoWorld to test on a device.
    public static readonly Uri ApiBaseUrl = new("https://localhost:7041/");
    public static readonly Uri WebBaseUrl = new("https://localhost:7211/");
#else
    // Set these to the deployed API and website before publishing.
    public static readonly Uri ApiBaseUrl = new("https://api.photocommunity.co.uk/");
    public static readonly Uri WebBaseUrl = new("https://photocommunity.co.uk/");
#endif

    public static Uri PrivacyUrl => new(WebBaseUrl, "Home/Privacy");
    public static Uri TermsUrl => new(WebBaseUrl, "Home/Terms");

    /// <summary>
    /// Development only: trust the ASP.NET Core dev certificate on localhost, which the Simulator doesn't know.
    /// Release builds use normal certificate validation everywhere.
    /// </summary>
    public static HttpMessageHandler CreateHttpHandler()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
            errors == System.Net.Security.SslPolicyErrors.None || request.RequestUri?.IsLoopback == true;
#endif
        return handler;
    }
}
