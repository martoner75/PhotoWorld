using Foundation;

namespace PhotoWorld.App;

/// <summary>
/// Apps built with the iOS 27 SDK must adopt the UIScene life cycle or they fail to launch. MAUI's scene delegate
/// creates the app's window; Info.plist (UIApplicationSceneManifest) points iOS at this class.
/// </summary>
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate;
