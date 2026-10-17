namespace PhotoWorld.Shared;

/// <summary>Relative API routes, shared so the UI client and the API controllers can't drift apart.</summary>
public static class ApiRoutes
{
    public const string ExternalLogin = "api/auth/external";
    public const string CurrentUser = "api/users/me";
    public const string Member = "api/users/{id:guid}";

    public const string Photos = "api/photos";
    public const string Photo = "api/photos/{id:guid}";
    public const string PhotoLike = "api/photos/{id:guid}/like";
    public const string PhotoComments = "api/photos/{id:guid}/comments";
    public const string PhotoComment = "api/photos/{id:guid}/comments/{commentId:guid}";
    public const string PhotoReports = "api/photos/{id:guid}/reports";
    public const string CommentReports = "api/photos/{id:guid}/comments/{commentId:guid}/reports";

    /// <summary>Fills in a route template's <c>{name:constraint}</c> placeholders, for use by API clients.</summary>
    public static string Format(string template, params (string Name, object Value)[] values)
    {
        foreach (var (name, value) in values)
        {
            var start = template.IndexOf("{" + name, StringComparison.Ordinal);
            var end = template.IndexOf('}', start);
            template = string.Concat(template.AsSpan(0, start), Uri.EscapeDataString(value.ToString()!), template.AsSpan(end + 1));
        }
        return template;
    }
}
