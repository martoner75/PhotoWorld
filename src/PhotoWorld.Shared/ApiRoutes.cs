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
    /// <summary>A public portfolio, readable without signing in.</summary>
    public const string PublicPortfolio = "api/portfolios/{slug}";
    /// <summary>GET / PUT the signed-in member's own portfolio settings.</summary>
    public const string MyPortfolio = "api/users/me/portfolio";
    public const string Photos = "api/photos";
    public const string PhotoCameraOptions = "api/photos/camera-options";
    public const string PhotoOfTheDay = "api/photo-of-the-day";
    public const string AdminPhotoOfTheDay = "api/admin/photo-of-the-day";
    /// <summary>One day on the schedule; {date} is yyyy-MM-dd (UTC).</summary>
    public const string AdminPhotoOfTheDayDate = "api/admin/photo-of-the-day/{date}";
    public const string Search = "api/search";
    /// <summary>The signed-in member's own insights (views, likes, followers).</summary>
    public const string Insights = "api/users/me/insights";
    public const string Explore = "api/explore";
    /// <summary>Photos whose owners linked them to a place, for the Spots map.</summary>
    public const string Spots = "api/spots";
    public const string Photo = "api/photos/{id:guid}";
    public const string PhotoLike = "api/photos/{id:guid}/like";
    /// <summary>POST (multipart "file") adds or replaces the unedited "before" version; DELETE removes it. Owner only.</summary>
    public const string PhotoBefore = "api/photos/{id:guid}/before";
    public const string PhotoComments = "api/photos/{id:guid}/comments";
    public const string PhotoComment = "api/photos/{id:guid}/comments/{commentId:guid}";
    public const string PhotoReports = "api/photos/{id:guid}/reports";
    /// <summary>GET the feedback panel; PUT / DELETE the caller's own feedback.</summary>
    public const string PhotoFeedback = "api/photos/{id:guid}/feedback";
    /// <summary>The photo's owner removing someone's feedback.</summary>
    public const string PhotoFeedbackItem = "api/photos/{id:guid}/feedback/{feedbackId:guid}";
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
    /// <summary>PUT registers the caller's phone for push notifications; DELETE (?token=) forgets it (signing out).</summary>
    public const string Devices = "api/devices";

    // Admins only.
    public const string AdminUsers = "api/admin/users";
    public const string AdminUserAdmin = "api/admin/users/{id:guid}/admin";
    public const string AdminUserSuspension = "api/admin/users/{id:guid}/suspension";
    /// <summary>Blog posts waiting for approval, oldest first.</summary>
    public const string AdminPendingPosts = "api/admin/blog-posts/pending";
    public const string AdminPostApproval = "api/admin/blog-posts/{id:guid}/approve";
    public const string AdminPostRejection = "api/admin/blog-posts/{id:guid}/reject";
    /// <summary>Photos waiting for approval, oldest first (photos in a post waiting for approval are reviewed with it).</summary>
    public const string AdminPendingPhotos = "api/admin/photos/pending";
    /// <summary>POST a list of photo ids to approve them all.</summary>
    public const string AdminPhotoApproval = "api/admin/photos/approve";
    public const string AdminPhotoRejection = "api/admin/photos/{id:guid}/reject";
    public const string AdminPendingClubs = "api/admin/clubs/pending";
    public const string AdminClubApproval = "api/admin/clubs/{id:guid}/approve";
    public const string AdminClubRejection = "api/admin/clubs/{id:guid}/reject";

    // Collections: without ownerId the caller's own (private ones included); with it, that member's public ones.
    public const string Collections = "api/collections";
    public const string Collection = "api/collections/{id:guid}";
    public const string CollectionPhoto = "api/collections/{id:guid}/photos/{photoId:guid}";
    /// <summary>The caller's collections, each saying whether it holds this photo.</summary>
    public const string PhotoCollections = "api/photos/{id:guid}/collections";

    /// <summary>GET ?list=upcoming|past|mine; POST creates.</summary>
    public const string Clubs = "api/clubs";
    public const string MyClubs = "api/clubs/mine";
    public const string Club = "api/clubs/{id:guid}";
    /// <summary>PUT joins (or asks to); DELETE leaves (or withdraws the request).</summary>
    public const string ClubMembership = "api/clubs/{id:guid}/membership";
    public const string ClubMembers = "api/clubs/{id:guid}/members";
    /// <summary>DELETE removes a member or turns down a request.</summary>
    public const string ClubMember = "api/clubs/{id:guid}/members/{userId:guid}";
    public const string ClubMemberApproval = "api/clubs/{id:guid}/members/{userId:guid}/approve";
    public const string ClubMemberRole = "api/clubs/{id:guid}/members/{userId:guid}/role";
    public const string ClubReports = "api/clubs/{id:guid}/reports";

    public const string Walks = "api/walks";
    public const string Walk = "api/walks/{id:guid}";
    public const string WalkCancel = "api/walks/{id:guid}/cancel";
    /// <summary>PUT: I'm going; DELETE: not any more.</summary>
    public const string WalkAttendance = "api/walks/{id:guid}/attendance";

    public const string Challenges = "api/challenges";
    public const string Challenge = "api/challenges/{id:guid}";
    public const string ChallengeEntries = "api/challenges/{id:guid}/entries";
    public const string ChallengeEntry = "api/challenges/{id:guid}/entries/{photoId:guid}";

    // Always the signed-in member's own places; there is deliberately no route to anyone else's.
    public const string Places = "api/places";
    public const string Place = "api/places/{id:guid}";
    public const string PlaceForecast = "api/places/{id:guid}/forecast";

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
