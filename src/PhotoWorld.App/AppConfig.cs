namespace PhotoWorld.App;

/// <summary>Where the app finds PhotoWorld.</summary>
public static class AppConfig
{
#if DEBUG
    // The iOS Simulator shares the Mac's network, so it reaches the API and website started with `dotnet run`.
    // A real iPhone can't: point these at a deployed PhotoWorld (or your Mac's LAN address) to test on a device.
    public static readonly Uri ApiBaseUrl = new("https://localhost:7041/");
    public static readonly Uri WebBaseUrl = new("https://localhost:7211/");
#else
    // Set these to the deployed API and website before publishing.
    public static readonly Uri ApiBaseUrl = new("https://api.photoworld.example/");
    public static readonly Uri WebBaseUrl = new("https://photoworld.example/");
#endif

    public static Uri PrivacyUrl => new(WebBaseUrl, "Home/Privacy");

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
