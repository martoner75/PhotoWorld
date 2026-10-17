using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Photos;
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
    Task<ApiResult<PhotoDetailDto>> UploadPhotoAsync(IFormFile file, string title, string? description, CancellationToken ct = default);
    Task<ApiResult<PhotoDetailDto>> UpdatePhotoAsync(Guid id, UpdatePhotoRequest request, CancellationToken ct = default);
    Task<bool> DeletePhotoAsync(Guid id, CancellationToken ct = default);

    Task<LikeStateDto?> SetLikeAsync(Guid id, bool liked, CancellationToken ct = default);
    Task<IReadOnlyList<CommentDto>> GetCommentsAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<CommentDto>> AddCommentAsync(Guid id, string text, CancellationToken ct = default);
    Task<bool> DeleteCommentAsync(Guid id, Guid commentId, CancellationToken ct = default);
}
