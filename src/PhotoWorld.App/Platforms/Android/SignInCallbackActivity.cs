using Android.App;
using Android.Content;
using Android.Content.PM;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.App;

/// <summary>
/// Receives the website's photoworld://auth redirect after sign-in and hands it to WebAuthenticator
/// (see <see cref="AppSignIn"/>). The redirect carries only a one-time code, useless without the app's verifier.
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = AppSignIn.CallbackScheme, DataHost = "auth")]
public class SignInCallbackActivity : WebAuthenticatorCallbackActivity;
