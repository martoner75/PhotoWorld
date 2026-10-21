namespace PhotoWorld.Shared;

/// <summary>Relative API routes, shared so the UI client and the API controllers can't drift apart.</summary>
public static class ApiRoutes
{
    public const string ExternalLogin = "api/auth/external";
    public const string CurrentUser = "api/users/me";
    public const string Member = "api/users/{id:guid}";
    public const string MemberFollow = "api/users/{id:guid}/follow";
    public const string MemberBlock = "api/users/{id:guid}/block";
    /// <summary>The members the signed-in member has blocked.</summary>
    public const string MyBlocks = "api/users/me/blocks";

    public const string Showcase = "api/showcase";
    public const string Photos = "api/photos";
    public const string PhotoCameraOptions = "api/photos/camera-options";
    /// <summary>Photos whose owners linked them to a place, for the Spots map.</summary>
    public const string Spots = "api/spots";
    public const string Photo = "api/photos/{id:guid}";
    public const string PhotoLike = "api/photos/{id:guid}/like";
    public const string PhotoComments = "api/photos/{id:guid}/comments";
    public const string PhotoComment = "api/photos/{id:guid}/comments/{commentId:guid}";
    public const string PhotoReports = "api/photos/{id:guid}/reports";
    public const string CommentReports = "api/photos/{id:guid}/comments/{commentId:guid}/reports";

    public const string Blog = "api/blogs/{userId:guid}";
    public const string BlogPosts = "api/blog-posts";
    public const string BlogPost = "api/blog-posts/{id:guid}";
    public const string BlogPostAttachments = "api/blog-posts/{id:guid}/attachments";
    public const string BlogPostAttachment = "api/blog-posts/{id:guid}/attachments/{attachmentId:guid}";
    public const string BlogPostComments = "api/blog-posts/{id:guid}/comments";
    public const string BlogPostComment = "api/blog-posts/{id:guid}/comments/{commentId:guid}";
    public const string BlogPostReports = "api/blog-posts/{id:guid}/reports";
    public const string BlogCommentReports = "api/blog-posts/{id:guid}/comments/{commentId:guid}/reports";

    // The signed-in member's notifications.
    public const string Activity = "api/activity";
    public const string ActivityUnread = "api/activity/unread";
    public const string ActivityRead = "api/activity/read";
    public const string ActivitySettings = "api/activity/settings";

    // Admins only.
    public const string AdminUsers = "api/admin/users";
    public const string AdminUserAdmin = "api/admin/users/{id:guid}/admin";
    public const string AdminUserSuspension = "api/admin/users/{id:guid}/suspension";

    // Collections: without ownerId the caller's own (private ones included); with it, that member's public ones.
    public const string Collections = "api/collections";
    public const string Collection = "api/collections/{id:guid}";
    public const string CollectionPhoto = "api/collections/{id:guid}/photos/{photoId:guid}";
    /// <summary>The caller's collections, each saying whether it holds this photo.</summary>
    public const string PhotoCollections = "api/photos/{id:guid}/collections";

    public const string Challenges = "api/challenges";
    public const string Challenge = "api/challenges/{id:guid}";
    public const string ChallengeEntries = "api/challenges/{id:guid}/entries";
    public const string ChallengeEntry = "api/challenges/{id:guid}/entries/{photoId:guid}";

    // Always the signed-in member's own places; there is deliberately no route to anyone else's.
    public const string Places = "api/places";
    public const string Place = "api/places/{id:guid}";

    // Email notifications about new blog posts: the member's own settings, and the anonymous links in emails.
    public const string MyNotifications = "api/users/me/notifications";
    public const string MyNotificationsConfirmation = "api/users/me/notifications/confirmation";
    public const string ConfirmNotifications = "api/notifications/confirm";
    public const string Unsubscribe = "api/notifications/unsubscribe";

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
