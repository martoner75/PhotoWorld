namespace PhotoWorld.Shared.Auth;

/// <summary>
/// External identity providers people can use to join PhotoWorld (currently Google only).
/// The names double as ASP.NET Core authentication scheme names in the Web project
/// and as the provider key the API stores against each user.
/// </summary>
public static class AuthProviders
{
    public const string Google = "Google";

    public static readonly IReadOnlyList<string> All = [Google];

    public static bool IsSupported(string? provider) =>
        provider is not null && All.Contains(provider, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string provider) =>
        All.First(p => string.Equals(p, provider, StringComparison.OrdinalIgnoreCase));
}
