using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Admin;
using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Shared.Notifications;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Places;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.Web.Services;

public sealed class PhotoWorldApiClient(HttpClient http, ILogger<PhotoWorldApiClient> logger) : IPhotoWorldApiClient
{
    public async Task<AuthResponse?> ExternalLoginAsync(ExternalLoginRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync(ApiRoutes.ExternalLogin, request, ct);
        if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<AuthResponse>(ct);
        if (res.StatusCode == HttpStatusCode.Forbidden) throw new AccountSuspendedException(await ProblemDetailAsync(res, ct));

        logger.LogWarning("API rejected {Provider} login: {Status} {Body}",
            request.Provider, (int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));
        return null;
    }

    public Task<UserProfileDto?> GetCurrentUserAsync(CancellationToken ct = default) =>
        GetOrNullAsync<UserProfileDto>(ApiRoutes.CurrentUser, ct);

    public async Task<bool> DeleteAccountAsync(CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync(ApiRoutes.CurrentUser, ct);
        return IsSuccess(res);
    }

    public Task<MemberProfileDto?> GetMemberAsync(Guid id, CancellationToken ct = default) =>
        GetOrNullAsync<MemberProfileDto>(ApiRoutes.Format(ApiRoutes.Member, ("id", id)), ct);

    public async Task<FollowStateDto?> SetFollowAsync(Guid id, bool follow, CancellationToken ct = default)
    {
        var url = ApiRoutes.Format(ApiRoutes.MemberFollow, ("id", id));
        using var res = follow ? await http.PostAsync(url, null, ct) : await http.DeleteAsync(url, ct);
        // 400: trying to follow yourself.
        if (res.StatusCode == HttpStatusCode.BadRequest) return null;
        return IsSuccess(res) ? await res.Content.ReadFromJsonAsync<FollowStateDto>(ct) : null;
    }

    public async Task<bool> SetBlockAsync(Guid id, bool block, CancellationToken ct = default)
    {
        var url = ApiRoutes.Format(ApiRoutes.MemberBlock, ("id", id));
        using var res = block ? await http.PostAsync(url, null, ct) : await http.DeleteAsync(url, ct);
        return res.StatusCode != HttpStatusCode.BadRequest && IsSuccess(res);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetBlockedMembersAsync(CancellationToken ct = default) =>
        await GetOrNullAsync<List<UserSummaryDto>>(ApiRoutes.MyBlocks, ct) ?? [];

    public async Task<PagedResult<PhotoSummaryDto>> GetPhotosAsync(int page, Guid? ownerId = null, bool following = false,
        PhotoSearch? search = null, CancellationToken ct = default)
    {
        var query = new List<(string Name, string Value)> { ("page", page.ToString()) };
        if (ownerId is not null) query.Add(("ownerId", ownerId.Value.ToString()));
        if (following) query.Add(("following", "true"));
        if (search is not null) query.AddRange(search.QueryValues());
        var url = $"{ApiRoutes.Photos}?" + string.Join("&", query.Select(q => $"{q.Name}={Uri.EscapeDataString(q.Value)}"));
        return (await GetOrNullAsync<PagedResult<PhotoSummaryDto>>(url, ct))!;
    }

    public async Task<CameraOptionsDto> GetCameraOptionsAsync(CancellationToken ct = default) =>
        await GetOrNullAsync<CameraOptionsDto>(ApiRoutes.PhotoCameraOptions, ct) ?? new CameraOptionsDto([], []);

    public Task<PhotoDetailDto?> GetPhotoAsync(Guid id, CancellationToken ct = default) =>
        GetOrNullAsync<PhotoDetailDto>(ApiRoutes.Format(ApiRoutes.Photo, ("id", id)), ct);

    public async Task<IReadOnlyList<ShowcasePhotoDto>> GetShowcaseAsync(CancellationToken ct = default) =>
        await GetOrNullAsync<List<ShowcasePhotoDto>>(ApiRoutes.Showcase, ct) ?? [];

    public async Task<ApiResult<PhotoDetailDto>> UploadPhotoAsync(IFormFile file, string title, string? description, bool showOnHome, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var type)
            ? type : new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", file.FileName);
        form.Add(new StringContent(title), "title");
        if (description is not null) form.Add(new StringContent(description), "description");
        form.Add(new StringContent(showOnHome ? "true" : "false"), "showOnHome");

        using var res = await http.PostAsync(ApiRoutes.Photos, form, ct);
        return await ReadResultAsync<PhotoDetailDto>(res, ct);
    }

    public async Task<ApiResult<PhotoDetailDto>> UpdatePhotoAsync(Guid id, UpdatePhotoRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync(ApiRoutes.Format(ApiRoutes.Photo, ("id", id)), request, ct);
        return await ReadResultAsync<PhotoDetailDto>(res, ct);
    }

    public async Task<bool> DeletePhotoAsync(Guid id, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync(ApiRoutes.Format(ApiRoutes.Photo, ("id", id)), ct);
        return IsSuccess(res);
    }

    public async Task<LikeStateDto?> SetLikeAsync(Guid id, bool liked, CancellationToken ct = default)
    {
        var url = ApiRoutes.Format(ApiRoutes.PhotoLike, ("id", id));
        using var res = liked ? await http.PostAsync(url, null, ct) : await http.DeleteAsync(url, ct);
        return IsSuccess(res) ? await res.Content.ReadFromJsonAsync<LikeStateDto>(ct) : null;
    }

    public async Task<IReadOnlyList<CommentDto>> GetCommentsAsync(Guid id, CancellationToken ct = default) =>
        await GetOrNullAsync<List<CommentDto>>(ApiRoutes.Format(ApiRoutes.PhotoComments, ("id", id)), ct) ?? [];

    public async Task<ApiResult<CommentDto>> AddCommentAsync(Guid id, string text, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync(ApiRoutes.Format(ApiRoutes.PhotoComments, ("id", id)),
            new CreateCommentRequest(text), ct);
        return await ReadResultAsync<CommentDto>(res, ct);
    }

    public async Task<bool> DeleteCommentAsync(Guid id, Guid commentId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync(
            ApiRoutes.Format(ApiRoutes.PhotoComment, ("id", id), ("commentId", commentId)), ct);
        return IsSuccess(res);
    }

    public Task<ReportOutcome> ReportAsync(Guid id, Guid? commentId, CreateReportRequest request, CancellationToken ct = default) =>
        SendReportAsync(commentId is null
            ? ApiRoutes.Format(ApiRoutes.PhotoReports, ("id", id))
            : ApiRoutes.Format(ApiRoutes.CommentReports, ("id", id), ("commentId", commentId)), request, ct);

    public Task<ReportOutcome> ReportBlogAsync(Guid postId, Guid? commentId, CreateReportRequest request, CancellationToken ct = default) =>
        SendReportAsync(commentId is null
            ? ApiRoutes.Format(ApiRoutes.BlogPostReports, ("id", postId))
            : ApiRoutes.Format(ApiRoutes.BlogCommentReports, ("id", postId), ("commentId", commentId)), request, ct);

    public Task<BlogDto?> GetBlogAsync(Guid userId, CancellationToken ct = default) =>
        GetOrNullAsync<BlogDto>(ApiRoutes.Format(ApiRoutes.Blog, ("userId", userId)), ct);

    public async Task<ApiResult<BlogDto>> SaveBlogAsync(Guid userId, SaveBlogRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync(ApiRoutes.Format(ApiRoutes.Blog, ("userId", userId)), request, ct);
        return await ReadResultAsync<BlogDto>(res, ct);
    }

    public async Task<PagedResult<BlogPostSummaryDto>> GetPostsAsync(int page, Guid? authorId = null, bool following = false,
        CancellationToken ct = default)
    {
        var url = $"{ApiRoutes.BlogPosts}?page={page}" + (authorId is null ? "" : $"&authorId={authorId}")
                  + (following ? "&following=true" : "");
        return (await GetOrNullAsync<PagedResult<BlogPostSummaryDto>>(url, ct))!;
    }

    public Task<BlogPostDto?> GetPostAsync(Guid id, CancellationToken ct = default) =>
        GetOrNullAsync<BlogPostDto>(ApiRoutes.Format(ApiRoutes.BlogPost, ("id", id)), ct);

    public async Task<ApiResult<BlogPostDto>> CreatePostAsync(SavePostRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync(ApiRoutes.BlogPosts, request, ct);
        return await ReadResultAsync<BlogPostDto>(res, ct);
    }

    public async Task<ApiResult<BlogPostDto>> UpdatePostAsync(Guid id, SavePostRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync(ApiRoutes.Format(ApiRoutes.BlogPost, ("id", id)), request, ct);
        return await ReadResultAsync<BlogPostDto>(res, ct);
    }

    public async Task<bool> DeletePostAsync(Guid id, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync(ApiRoutes.Format(ApiRoutes.BlogPost, ("id", id)), ct);
        return IsSuccess(res);
    }

    public async Task<ApiResult<BlogAttachmentDto>> AddBlogAttachmentAsync(Guid postId, IFormFile file, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(BlogLimits.AttachmentContentType);
        form.Add(fileContent, "file", file.FileName);

        using var res = await http.PostAsync(ApiRoutes.Format(ApiRoutes.BlogPostAttachments, ("id", postId)), form, ct);
        return await ReadResultAsync<BlogAttachmentDto>(res, ct);
    }

    public async Task<bool> DeleteBlogAttachmentAsync(Guid postId, Guid attachmentId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync(
            ApiRoutes.Format(ApiRoutes.BlogPostAttachment, ("id", postId), ("attachmentId", attachmentId)), ct);
        return IsSuccess(res);
    }

    public async Task<IReadOnlyList<BlogCommentDto>> GetBlogCommentsAsync(Guid postId, CancellationToken ct = default) =>
        await GetOrNullAsync<List<BlogCommentDto>>(ApiRoutes.Format(ApiRoutes.BlogPostComments, ("id", postId)), ct) ?? [];

    public async Task<ApiResult<BlogCommentDto>> AddBlogCommentAsync(Guid postId, string text, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync(ApiRoutes.Format(ApiRoutes.BlogPostComments, ("id", postId)),
            new CreateBlogCommentRequest(text), ct);
        return await ReadResultAsync<BlogCommentDto>(res, ct);
    }

    public async Task<bool> DeleteBlogCommentAsync(Guid postId, Guid commentId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync(
            ApiRoutes.Format(ApiRoutes.BlogPostComment, ("id", postId), ("commentId", commentId)), ct);
        return IsSuccess(res);
    }

    public async Task<IReadOnlyList<ChallengeSummaryDto>> GetChallengesAsync(CancellationToken ct = default) =>
        await GetOrNullAsync<List<ChallengeSummaryDto>>(ApiRoutes.Challenges, ct) ?? [];

    public Task<ChallengeDto?> GetChallengeAsync(Guid id, CancellationToken ct = default) =>
        GetOrNullAsync<ChallengeDto>(ApiRoutes.Format(ApiRoutes.Challenge, ("id", id)), ct);

    public async Task<string?> EnterChallengeAsync(Guid id, Guid photoId, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync(ApiRoutes.Format(ApiRoutes.ChallengeEntries, ("id", id)),
            new EnterChallengeRequest(photoId), ct);
        if (res.StatusCode == HttpStatusCode.BadRequest) return await ProblemDetailAsync(res, ct);
        return IsSuccess(res) ? null : "That challenge no longer exists.";
    }

    public async Task<string?> WithdrawChallengeEntryAsync(Guid id, Guid photoId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync(
            ApiRoutes.Format(ApiRoutes.ChallengeEntry, ("id", id), ("photoId", photoId)), ct);
        if (res.StatusCode == HttpStatusCode.BadRequest) return await ProblemDetailAsync(res, ct);
        return IsSuccess(res) ? null : "That entry no longer exists.";
    }

    public Task<PagedResult<AdminUserDto>?> GetAdminUsersAsync(int page, string? search, CancellationToken ct = default) =>
        GetOrNullAsync<PagedResult<AdminUserDto>>($"{ApiRoutes.AdminUsers}?page={page}"
            + (string.IsNullOrWhiteSpace(search) ? "" : $"&search={Uri.EscapeDataString(search.Trim())}"), ct);

    public async Task<string?> SetAdminAsync(Guid id, bool isAdmin, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync(ApiRoutes.Format(ApiRoutes.AdminUserAdmin, ("id", id)), new SetAdminRequest(isAdmin), ct);
        if (res.StatusCode == HttpStatusCode.BadRequest) return await ProblemDetailAsync(res, ct);
        return IsSuccess(res) ? null : "That member no longer exists, or you're not an admin.";
    }

    public async Task<string?> SetSuspensionAsync(Guid id, SetSuspensionRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync(ApiRoutes.Format(ApiRoutes.AdminUserSuspension, ("id", id)), request, ct);
        if (res.StatusCode == HttpStatusCode.BadRequest) return await ProblemDetailAsync(res, ct);
        return IsSuccess(res) ? null : "That member no longer exists, or you're not an admin.";
    }

    public async Task<ApiResult<ChallengeSummaryDto>> CreateChallengeAsync(SaveChallengeRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync(ApiRoutes.Challenges, request, ct);
        return await ReadResultAsync<ChallengeSummaryDto>(res, ct, "Only admins can manage challenges.");
    }

    public async Task<ApiResult<ChallengeSummaryDto>> UpdateChallengeAsync(Guid id, SaveChallengeRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync(ApiRoutes.Format(ApiRoutes.Challenge, ("id", id)), request, ct);
        return await ReadResultAsync<ChallengeSummaryDto>(res, ct, "That challenge no longer exists, or you can't change it.");
    }

    public async Task<bool> DeleteChallengeAsync(Guid id, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync(ApiRoutes.Format(ApiRoutes.Challenge, ("id", id)), ct);
        return IsSuccess(res);
    }

    public async Task<IReadOnlyList<PlaceDto>> GetPlacesAsync(CancellationToken ct = default) =>
        await GetOrNullAsync<List<PlaceDto>>(ApiRoutes.Places, ct) ?? [];

    public async Task<ApiResult<PlaceDto>> CreatePlaceAsync(SavePlaceRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync(ApiRoutes.Places, request, ct);
        return await ReadResultAsync<PlaceDto>(res, ct);
    }

    public async Task<ApiResult<PlaceDto>> UpdatePlaceAsync(Guid id, SavePlaceRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync(ApiRoutes.Format(ApiRoutes.Place, ("id", id)), request, ct);
        return await ReadResultAsync<PlaceDto>(res, ct, "That place no longer exists.");
    }

    public async Task<bool> DeletePlaceAsync(Guid id, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync(ApiRoutes.Format(ApiRoutes.Place, ("id", id)), ct);
        return IsSuccess(res);
    }

    public Task<EmailNotificationsDto?> GetNotificationsAsync(CancellationToken ct = default) =>
        GetOrNullAsync<EmailNotificationsDto>(ApiRoutes.MyNotifications, ct);

    public async Task<ApiResult<EmailNotificationsDto>> SaveNotificationsAsync(SaveEmailNotificationsRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync(ApiRoutes.MyNotifications, request, ct);
        // Saved, but the confirmation email couldn't be sent: the API explains in the problem's detail.
        if (res.StatusCode == HttpStatusCode.BadGateway) return ApiResult<EmailNotificationsDto>.Failure(await ProblemDetailAsync(res, ct));
        return await ReadResultAsync<EmailNotificationsDto>(res, ct, "We couldn't save your notification settings.");
    }

    public async Task<string?> ResendNotificationConfirmationAsync(CancellationToken ct = default)
    {
        using var res = await http.PostAsync(ApiRoutes.MyNotificationsConfirmation, null, ct);
        if (res.StatusCode == HttpStatusCode.BadGateway) return await ProblemDetailAsync(res, ct);
        return IsSuccess(res) ? null : "That email address is already confirmed.";
    }

    public async Task<bool> ConfirmNotificationsAsync(string token, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync(ApiRoutes.ConfirmNotifications, new NotificationTokenRequest(token), ct);
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> UnsubscribeAsync(string token, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync(ApiRoutes.Unsubscribe, new NotificationTokenRequest(token), ct);
        return res.IsSuccessStatusCode;
    }

    private static async Task<string> ProblemDetailAsync(HttpResponseMessage res, CancellationToken ct) =>
        (await res.Content.ReadFromJsonAsync<ProblemDetails>(ct))?.Detail ?? "Something went wrong. Please try again.";

    private async Task<ReportOutcome> SendReportAsync(string url, CreateReportRequest request, CancellationToken ct)
    {
        using var res = await http.PostAsJsonAsync(url, request, ct);
        return res.StatusCode switch
        {
            HttpStatusCode.Conflict => ReportOutcome.AlreadyReported,
            HttpStatusCode.BadRequest => ReportOutcome.NotAllowed,
            _ => IsSuccess(res) ? ReportOutcome.Reported : ReportOutcome.NotFound,
        };
    }

    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken ct)
    {
        using var res = await http.GetAsync(url, ct);
        return IsSuccess(res) ? await res.Content.ReadFromJsonAsync<T>(ct) : default;
    }

    /// <summary>True on 2xx; false on 403/404; throws on 401 and on unexpected server errors.</summary>
    private static bool IsSuccess(HttpResponseMessage res)
    {
        if (res.StatusCode == HttpStatusCode.Unauthorized) throw new ApiUnauthorizedException();
        if (res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return false;
        res.EnsureSuccessStatusCode();
        return true;
    }

    private static async Task<ApiResult<T>> ReadResultAsync<T>(HttpResponseMessage res, CancellationToken ct,
        string notFound = "That photo no longer exists, or you can't change it.")
    {
        if (res.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await res.Content.ReadFromJsonAsync<ValidationProblemDetails>(ct);
            return problem?.Errors.Count > 0
                ? ApiResult<T>.Failure(problem.Errors.AsReadOnly())
                : ApiResult<T>.Failure(problem?.Detail ?? "The request was not valid.");
        }
        if (!IsSuccess(res)) return ApiResult<T>.Failure(notFound);
        return ApiResult<T>.Success((await res.Content.ReadFromJsonAsync<T>(ct))!);
    }
}
