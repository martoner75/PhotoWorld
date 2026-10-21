namespace PhotoWorld.Shared.Auth;

/// <summary>Custom claim types shared by the API (JWT) and the UI (auth cookie).</summary>
public static class PhotoWorldClaimTypes
{
    public const string AvatarUrl = "photoworld:avatar";
    public const string Provider = "photoworld:provider";
    public const string IsAdmin = "photoworld:admin";
}
