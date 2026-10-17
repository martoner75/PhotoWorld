using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Photos;
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

    public async Task<ApiResult<PhotoDetailDto>> UploadPhotoAsync(IFormFile file, string title, string? description, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var type)
            ? type : new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", file.FileName);
        form.Add(new StringContent(title), "title");
        if (description is not null) form.Add(new StringContent(description), "description");

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

    public async Task<ReportOutcome> ReportAsync(Guid id, Guid? commentId, CreateReportRequest request, CancellationToken ct = default)
    {
        var url = commentId is null
            ? ApiRoutes.Format(ApiRoutes.PhotoReports, ("id", id))
            : ApiRoutes.Format(ApiRoutes.CommentReports, ("id", id), ("commentId", commentId));
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

    private static async Task<ApiResult<T>> ReadResultAsync<T>(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await res.Content.ReadFromJsonAsync<ValidationProblemDetails>(ct);
            return problem?.Errors.Count > 0
                ? ApiResult<T>.Failure(problem.Errors.AsReadOnly())
                : ApiResult<T>.Failure(problem?.Detail ?? "The request was not valid.");
        }
        if (!IsSuccess(res)) return ApiResult<T>.Failure("That photo no longer exists, or you can't change it.");
        return ApiResult<T>.Success((await res.Content.ReadFromJsonAsync<T>(ct))!);
    }
}
