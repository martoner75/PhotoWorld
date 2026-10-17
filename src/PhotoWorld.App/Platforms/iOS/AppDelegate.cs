using Foundation;
using UIKit;

namespace PhotoWorld.App;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // Hands the photoworld://auth callback to WebAuthenticator (sign-in through the website).
    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options) =>
        Platform.OpenUrl(application, url, options) || base.OpenUrl(application, url, options);
}
