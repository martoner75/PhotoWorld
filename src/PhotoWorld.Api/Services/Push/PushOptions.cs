namespace PhotoWorld.Api.Services.Push;

/// <summary>
/// Push notification keys (user-secrets or environment; never appsettings.json). Each platform is off until its keys
/// are set, and PushDispatcher doesn't run at all while both are off. See README → Push notifications.
/// </summary>
public sealed class PushOptions
{
    public const string Section = "Push";

    public ApnsOptions Apns { get; set; } = new();
    public FcmOptions Fcm { get; set; } = new();

    /// <summary>How often new activity is sent.</summary>
    public int PollSeconds { get; set; } = 20;
}

/// <summary>Apple Push Notification service, token-based (a .p8 key from the Apple Developer account).</summary>
public sealed class ApnsOptions
{
    public string TeamId { get; set; } = "";
    public string KeyId { get; set; } = "";
    /// <summary>The app's bundle id (the APNs topic).</summary>
    public string BundleId { get; set; } = "";
    /// <summary>The contents of the AuthKey_XXXX.p8 file (PEM).</summary>
    public string PrivateKey { get; set; } = "";
    /// <summary>Development builds talk to the sandbox server.</summary>
    public bool UseSandbox { get; set; }

    public bool IsConfigured => TeamId.Length > 0 && KeyId.Length > 0 && BundleId.Length > 0 && PrivateKey.Length > 0;
}

/// <summary>Firebase Cloud Messaging HTTP v1 (a service-account key from the Firebase project).</summary>
public sealed class FcmOptions
{
    /// <summary>The whole service-account JSON file's contents.</summary>
    public string ServiceAccountJson { get; set; } = "";

    public bool IsConfigured => ServiceAccountJson.Length > 0;
}
