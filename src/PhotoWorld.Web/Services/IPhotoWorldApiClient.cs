using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Blogs;
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
    /// <summary>Exchanges a provider access token for a PhotoWorld session. Null when the API rejects it.</summary>
    Task<AuthResponse?> ExternalLoginAsync(ExternalLoginRequest request, CancellationToken ct = default);

    Task<UserProfileDto?> GetCurrentUserAsync(CancellationToken ct = default);
    Task<bool> DeleteAccountAsync(CancellationToken ct = default);
    Task<MemberProfileDto?> GetMemberAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<PhotoSummaryDto>> GetPhotosAsync(int page, Guid? ownerId = null, CancellationToken ct = default);
    Task<PhotoDetailDto?> GetPhotoAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<PhotoDetailDto>> UploadPhotoAsync(IFormFile file, string title, string? description, bool showOnHome, CancellationToken ct = default);

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
    Task<PagedResult<BlogPostSummaryDto>> GetPostsAsync(int page, Guid? authorId = null, CancellationToken ct = default);
    Task<BlogPostDto?> GetPostAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<BlogPostDto>> CreatePostAsync(SavePostRequest request, CancellationToken ct = default);
    Task<ApiResult<BlogPostDto>> UpdatePostAsync(Guid id, SavePostRequest request, CancellationToken ct = default);
    Task<bool> DeletePostAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BlogCommentDto>> GetBlogCommentsAsync(Guid postId, CancellationToken ct = default);
    Task<ApiResult<BlogCommentDto>> AddBlogCommentAsync(Guid postId, string text, CancellationToken ct = default);
    Task<bool> DeleteBlogCommentAsync(Guid postId, Guid commentId, CancellationToken ct = default);

    /// <summary>Reports a blog post, or one of its comments when <paramref name="commentId"/> is set.</summary>
    Task<ReportOutcome> ReportBlogAsync(Guid postId, Guid? commentId, CreateReportRequest request, CancellationToken ct = default);

    // Places (always the signed-in member's own)
    Task<IReadOnlyList<PlaceDto>> GetPlacesAsync(CancellationToken ct = default);
    Task<ApiResult<PlaceDto>> CreatePlaceAsync(SavePlaceRequest request, CancellationToken ct = default);
    Task<ApiResult<PlaceDto>> UpdatePlaceAsync(Guid id, SavePlaceRequest request, CancellationToken ct = default);
    Task<bool> DeletePlaceAsync(Guid id, CancellationToken ct = default);
}
