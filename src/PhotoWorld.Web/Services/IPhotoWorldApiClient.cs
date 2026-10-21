using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Admin;
using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Shared.Collections;
using PhotoWorld.Shared.Notifications;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Places;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.Web.Services;

/// <summary>
/// Typed client for PhotoWorld.Api. Calls made for a signed-in user throw <see cref="ApiUnauthorizedException"/>
/// when the API rejects their token; "not found" and "not allowed" come back as null / false.
/// </summary>
public interface IPhotoWorldApiClient
{
    /// <summary>
    /// Exchanges a provider access token for a PhotoWorld session. Null when the API rejects it; throws
    /// <see cref="AccountSuspendedException"/> for a suspended account.
    /// </summary>
    Task<AuthResponse?> ExternalLoginAsync(ExternalLoginRequest request, CancellationToken ct = default);

    Task<UserProfileDto?> GetCurrentUserAsync(CancellationToken ct = default);
    Task<bool> DeleteAccountAsync(CancellationToken ct = default);
    Task<MemberProfileDto?> GetMemberAsync(Guid id, CancellationToken ct = default);
    Task<FollowStateDto?> SetFollowAsync(Guid id, bool follow, CancellationToken ct = default);
    /// <summary>False when the member doesn't exist (or is the caller).</summary>
    Task<bool> SetBlockAsync(Guid id, bool block, CancellationToken ct = default);
    Task<IReadOnlyList<UserSummaryDto>> GetBlockedMembersAsync(CancellationToken ct = default);

    /// <summary>Newest photos; <paramref name="following"/> limits them to members the caller follows.</summary>
    Task<PagedResult<PhotoSummaryDto>> GetPhotosAsync(int page, Guid? ownerId = null, bool following = false,
        PhotoSearch? search = null, CancellationToken ct = default);
    Task<CameraOptionsDto> GetCameraOptionsAsync(CancellationToken ct = default);
    Task<PhotoDetailDto?> GetPhotoAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<PhotoDetailDto>> UploadPhotoAsync(IFormFile file, string title, string? description, bool showOnHome, Guid? placeId = null,
        string? tags = null, bool wantsFeedback = false, CancellationToken ct = default);
    Task<PhotoFeedbackDto?> GetFeedbackAsync(Guid photoId, CancellationToken ct = default);
    /// <summary>Null on success, otherwise a message for the member.</summary>
    Task<string?> SaveFeedbackAsync(Guid photoId, SaveFeedbackRequest request, CancellationToken ct = default);
    /// <summary>Deletes the caller's feedback, or (for the photo's owner) <paramref name="feedbackId"/>.</summary>
    Task<bool> DeleteFeedbackAsync(Guid photoId, Guid? feedbackId, CancellationToken ct = default);
    Task<SearchResultsDto> SearchAsync(string query, CancellationToken ct = default);
    /// <summary>Today's photo of the day (or the latest), or null.</summary>
    Task<PhotoOfTheDayDto?> GetPhotoOfTheDayAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PhotoOfTheDayScheduleDto>?> GetPhotoOfTheDayScheduleAsync(CancellationToken ct = default);
    /// <summary>Null on success, otherwise a message for the admin.</summary>
    Task<string?> SetPhotoOfTheDayAsync(DateOnly date, SetPhotoOfTheDayRequest request, CancellationToken ct = default);
    Task<bool> ClearPhotoOfTheDayAsync(DateOnly date, CancellationToken ct = default);
    Task<ExploreDto> ExploreAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PhotoSpotDto>> GetSpotsAsync(Guid? ownerId = null, CancellationToken ct = default);

    /// <summary>Photos featured on the public home page. Works without signing in.</summary>
    Task<IReadOnlyList<ShowcasePhotoDto>> GetShowcaseAsync(CancellationToken ct = default);
    Task<ApiResult<PhotoDetailDto>> UpdatePhotoAsync(Guid id, UpdatePhotoRequest request, CancellationToken ct = default);
    Task<bool> DeletePhotoAsync(Guid id, CancellationToken ct = default);

    Task<LikeStateDto?> SetLikeAsync(Guid id, bool liked, CancellationToken ct = default);
    Task<IReadOnlyList<CommentDto>> GetCommentsAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<CommentDto>> AddCommentAsync(Guid id, string text, CancellationToken ct = default);
    Task<bool> DeleteCommentAsync(Guid id, Guid commentId, CancellationToken ct = default);

    /// <summary>Reports a photo, or one of its comments when <paramref name="commentId"/> is set.</summary>
    Task<ReportOutcome> ReportAsync(Guid id, Guid? commentId, CreateReportRequest request, CancellationToken ct = default);

    // Blogs
    Task<BlogDto?> GetBlogAsync(Guid userId, CancellationToken ct = default);
    Task<ApiResult<BlogDto>> SaveBlogAsync(Guid userId, SaveBlogRequest request, CancellationToken ct = default);
    Task<PagedResult<BlogPostSummaryDto>> GetPostsAsync(int page, Guid? authorId = null, bool following = false, CancellationToken ct = default);
    Task<BlogPostDto?> GetPostAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<BlogPostDto>> CreatePostAsync(SavePostRequest request, CancellationToken ct = default);
    Task<ApiResult<BlogPostDto>> UpdatePostAsync(Guid id, SavePostRequest request, CancellationToken ct = default);
    Task<bool> DeletePostAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<BlogAttachmentDto>> AddBlogAttachmentAsync(Guid postId, IFormFile file, CancellationToken ct = default);
    Task<bool> DeleteBlogAttachmentAsync(Guid postId, Guid attachmentId, CancellationToken ct = default);
    Task<IReadOnlyList<BlogCommentDto>> GetBlogCommentsAsync(Guid postId, CancellationToken ct = default);
    Task<ApiResult<BlogCommentDto>> AddBlogCommentAsync(Guid postId, string text, CancellationToken ct = default);
    Task<bool> DeleteBlogCommentAsync(Guid postId, Guid commentId, CancellationToken ct = default);

    /// <summary>Reports a blog post, or one of its comments when <paramref name="commentId"/> is set.</summary>
    Task<ReportOutcome> ReportBlogAsync(Guid postId, Guid? commentId, CreateReportRequest request, CancellationToken ct = default);

    // Photo challenges
    Task<IReadOnlyList<ChallengeSummaryDto>> GetChallengesAsync(CancellationToken ct = default);
    Task<ChallengeDto?> GetChallengeAsync(Guid id, CancellationToken ct = default);
    /// <summary>Null on success, otherwise a message for the member.</summary>
    Task<string?> EnterChallengeAsync(Guid id, Guid photoId, CancellationToken ct = default);
    /// <summary>Null on success, otherwise a message for the member.</summary>
    Task<string?> WithdrawChallengeEntryAsync(Guid id, Guid photoId, CancellationToken ct = default);

    // Collections
    /// <summary>The caller's own collections, or <paramref name="ownerId"/>'s public ones.</summary>
    Task<IReadOnlyList<CollectionSummaryDto>> GetCollectionsAsync(Guid? ownerId = null, CancellationToken ct = default);
    Task<CollectionDto?> GetCollectionAsync(Guid id, int page, CancellationToken ct = default);
    Task<ApiResult<CollectionSummaryDto>> CreateCollectionAsync(SaveCollectionRequest request, CancellationToken ct = default);
    Task<ApiResult<CollectionSummaryDto>> UpdateCollectionAsync(Guid id, SaveCollectionRequest request, CancellationToken ct = default);
    Task<bool> DeleteCollectionAsync(Guid id, CancellationToken ct = default);
    /// <summary>Null on success, otherwise a message for the member.</summary>
    Task<string?> SetCollectionPhotoAsync(Guid id, Guid photoId, bool inCollection, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionChoiceDto>> GetCollectionChoicesAsync(Guid photoId, CancellationToken ct = default);

    // Activity (the signed-in member's notifications)
    Task<PagedResult<ActivityDto>> GetActivityAsync(int page, CancellationToken ct = default);
    Task<int> GetUnreadActivityAsync(CancellationToken ct = default);
    Task MarkActivityReadAsync(CancellationToken ct = default);
    Task<ActivitySettingsDto> GetActivitySettingsAsync(CancellationToken ct = default);
    Task<ActivitySettingsDto?> SaveActivitySettingsAsync(ActivitySettingsDto settings, CancellationToken ct = default);

    // Admins only
    Task<PagedResult<AdminUserDto>?> GetAdminUsersAsync(int page, string? search, CancellationToken ct = default);
    /// <summary>Null on success, otherwise a message for the admin.</summary>
    Task<string?> SetAdminAsync(Guid id, bool isAdmin, CancellationToken ct = default);
    /// <summary>Null on success, otherwise a message for the admin.</summary>
    Task<string?> SetSuspensionAsync(Guid id, SetSuspensionRequest request, CancellationToken ct = default);
    Task<ApiResult<ChallengeSummaryDto>> CreateChallengeAsync(SaveChallengeRequest request, CancellationToken ct = default);
    Task<ApiResult<ChallengeSummaryDto>> UpdateChallengeAsync(Guid id, SaveChallengeRequest request, CancellationToken ct = default);
    Task<bool> DeleteChallengeAsync(Guid id, CancellationToken ct = default);

    // Places (always the signed-in member's own)
    Task<IReadOnlyList<PlaceDto>> GetPlacesAsync(CancellationToken ct = default);
    Task<ApiResult<PlaceDto>> CreatePlaceAsync(SavePlaceRequest request, CancellationToken ct = default);
    Task<ApiResult<PlaceDto>> UpdatePlaceAsync(Guid id, SavePlaceRequest request, CancellationToken ct = default);
    Task<bool> DeletePlaceAsync(Guid id, CancellationToken ct = default);

    // Email notifications about new blog posts
    Task<EmailNotificationsDto?> GetNotificationsAsync(CancellationToken ct = default);
    Task<ApiResult<EmailNotificationsDto>> SaveNotificationsAsync(SaveEmailNotificationsRequest request, CancellationToken ct = default);
    /// <summary>Null on success, otherwise a message for the member.</summary>
    Task<string?> ResendNotificationConfirmationAsync(CancellationToken ct = default);

    /// <summary>From the link in a confirmation email; works without signing in. False if the link is unknown or expired.</summary>
    Task<bool> ConfirmNotificationsAsync(string token, CancellationToken ct = default);
    /// <summary>From the link in a notification email; works without signing in. False if the link is unknown.</summary>
    Task<bool> UnsubscribeAsync(string token, CancellationToken ct = default);
}
