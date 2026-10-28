namespace PhotoWorld.App;

/// <summary>Where the app finds PhotoWorld.</summary>
public static class AppConfig
{
    // The deployed PhotoWorld: IIS sub-applications, so keep the trailing slash or the /API and /APP prefixes are lost.
    static readonly Uri ServerApiBaseUrl = new("https://photocommunity.co.uk/API/");
    static readonly Uri ServerWebBaseUrl = new("https://photocommunity.co.uk/APP/");

#if DEBUG
    // The iOS Simulator shares the Mac's network, so it reaches the API and website started with `dotnet run`.
    // The Android emulator does too after `adb reverse tcp:7041 tcp:7041` and `adb reverse tcp:7211 tcp:7211`
    // (localhost matters: it's the sign-in redirect registered with Google). Real phones can't, so a Debug build
    // installed on a device uses the deployed server like a Release build.
    static bool UseLocalServer => DeviceInfo.Current.DeviceType == DeviceType.Virtual;

    public static readonly Uri ApiBaseUrl = UseLocalServer ? new("https://localhost:7041/") : ServerApiBaseUrl;
    public static readonly Uri WebBaseUrl = UseLocalServer ? new("https://localhost:7211/") : ServerWebBaseUrl;
#else
    public static readonly Uri ApiBaseUrl = ServerApiBaseUrl;
    public static readonly Uri WebBaseUrl = ServerWebBaseUrl;
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
