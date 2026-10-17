namespace PhotoWorld.Shared;

/// <summary>Relative API routes, shared so the UI client and the API controllers can't drift apart.</summary>
public static class ApiRoutes
{
    public const string ExternalLogin = "api/auth/external";
    public const string CurrentUser = "api/users/me";
}
