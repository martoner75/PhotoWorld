using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Blogs;
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

    public async Task<PagedResult<PhotoSummaryDto>> GetPhotosAsync(int page, Guid? ownerId = null, CancellationToken ct = default)
    {
        var url = $"{ApiRoutes.Photos}?page={page}" + (ownerId is null ? "" : $"&ownerId={ownerId}");
        return (await GetOrNullAsync<PagedResult<PhotoSummaryDto>>(url, ct))!;
    }

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

    public async Task<PagedResult<BlogPostSummaryDto>> GetPostsAsync(int page, Guid? authorId = null, CancellationToken ct = default)
    {
        var url = $"{ApiRoutes.BlogPosts}?page={page}" + (authorId is null ? "" : $"&authorId={authorId}");
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
