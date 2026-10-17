namespace PhotoWorld.Web.Authentication;

public static class AuthSchemes
{
    /// <summary>Short-lived cookie that holds the provider's result between the OAuth callback and our own sign-in.</summary>
    public const string External = "PhotoWorld.External";

    /// <summary>Name under which the PhotoWorld API JWT is stored in the auth cookie.</summary>
    public const string ApiAccessToken = "api_access_token";

    /// <summary>AuthenticationProperties item carrying the provider through the OAuth round-trip.</summary>
    public const string ProviderItem = "LoginProvider";
}
