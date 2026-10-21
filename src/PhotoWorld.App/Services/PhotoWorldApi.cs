using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Shared.Collections;
using PhotoWorld.Shared.Notifications;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Places;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.App.Services;

/// <summary>
/// Typed client for PhotoWorld.Api, the app's counterpart of the website's client. Failures surface as
/// <see cref="ApiException"/> with a message fit to show the member.
/// </summary>
public sealed class PhotoWorldApi(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ----- Account -----
    public Task<UserProfileDto> GetMeAsync(CancellationToken ct = default) => GetAsync<UserProfileDto>(ApiRoutes.CurrentUser, ct);

    public Task DeleteAccountAsync(CancellationToken ct = default) => SendAsync(HttpMethod.Delete, ApiRoutes.CurrentUser, null, ct);

    // ----- Members -----
    public Task<MemberProfileDto> GetMemberAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<MemberProfileDto>(WithId(ApiRoutes.Member, id), ct);

    public Task<FollowStateDto> SetFollowAsync(Guid id, bool follow, CancellationToken ct = default) =>
        SendAsync<FollowStateDto>(follow ? HttpMethod.Post : HttpMethod.Delete, WithId(ApiRoutes.MemberFollow, id), null, ct);

    public Task SetBlockAsync(Guid id, bool block, CancellationToken ct = default) =>
        SendAsync(block ? HttpMethod.Post : HttpMethod.Delete, WithId(ApiRoutes.MemberBlock, id), null, ct);

    public Task<List<UserSummaryDto>> GetBlockedMembersAsync(CancellationToken ct = default) =>
        GetAsync<List<UserSummaryDto>>(ApiRoutes.MyBlocks, ct);

    // ----- Photos -----
    /// <summary>Newest photos: optionally one member's, only followed members', and/or matching camera settings.</summary>
    public Task<PagedResult<PhotoSummaryDto>> GetPhotosAsync(int page, Guid? ownerId = null, bool following = false,
        PhotoSearch? search = null, CancellationToken ct = default)
    {
        var query = new List<(string Name, string Value)> { ("page", page.ToString()) };
        if (ownerId is not null) query.Add(("ownerId", ownerId.Value.ToString()));
        if (following) query.Add(("following", "true"));
        if (search is not null) query.AddRange(search.QueryValues());
        return GetAsync<PagedResult<PhotoSummaryDto>>(
            $"{ApiRoutes.Photos}?" + string.Join("&", query.Select(q => $"{q.Name}={Uri.EscapeDataString(q.Value)}")), ct);
    }

    public Task<SearchResultsDto> SearchAsync(string query, CancellationToken ct = default) =>
        GetAsync<SearchResultsDto>($"{ApiRoutes.Search}?q={Uri.EscapeDataString(query)}", ct);

    public Task<ExploreDto> ExploreAsync(CancellationToken ct = default) => GetAsync<ExploreDto>(ApiRoutes.Explore, ct);

    public Task<List<PhotoSpotDto>> GetSpotsAsync(CancellationToken ct = default) =>
        GetAsync<List<PhotoSpotDto>>(ApiRoutes.Spots, ct);

    public Task<CameraOptionsDto> GetCameraOptionsAsync(CancellationToken ct = default) =>
        GetAsync<CameraOptionsDto>(ApiRoutes.PhotoCameraOptions, ct);

    public Task<PhotoDetailDto> GetPhotoAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<PhotoDetailDto>(WithId(ApiRoutes.Photo, id), ct);

    public async Task<PhotoDetailDto> UploadPhotoAsync(Stream file, string fileName, string contentType, string title,
        string? description, bool showOnHome, Guid? placeId = null, string? tags = null, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var content = new StreamContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "file", fileName);
        form.Add(new StringContent(title), "title");
        if (!string.IsNullOrWhiteSpace(description)) form.Add(new StringContent(description), "description");
        form.Add(new StringContent(showOnHome ? "true" : "false"), "showOnHome");
        if (placeId is not null) form.Add(new StringContent(placeId.Value.ToString()), "placeId");
        if (!string.IsNullOrWhiteSpace(tags)) form.Add(new StringContent(tags), "tags");
        return await SendAsync<PhotoDetailDto>(HttpMethod.Post, ApiRoutes.Photos, form, ct);
    }

    public Task<LikeStateDto> SetLikeAsync(Guid id, bool liked, CancellationToken ct = default) =>
        SendAsync<LikeStateDto>(liked ? HttpMethod.Post : HttpMethod.Delete, WithId(ApiRoutes.PhotoLike, id), null, ct);

    public Task<List<CommentDto>> GetCommentsAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<List<CommentDto>>(WithId(ApiRoutes.PhotoComments, id), ct);

    public Task<CommentDto> AddCommentAsync(Guid id, string text, CancellationToken ct = default) =>
        SendAsync<CommentDto>(HttpMethod.Post, WithId(ApiRoutes.PhotoComments, id), JsonContent.Create(new CreateCommentRequest(text), options: Json), ct);

    public Task DeleteCommentAsync(Guid id, Guid commentId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, ApiRoutes.Format(ApiRoutes.PhotoComment, ("id", id), ("commentId", commentId)), null, ct);

    public Task ReportPhotoAsync(Guid id, Guid? commentId, CreateReportRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, commentId is null
            ? WithId(ApiRoutes.PhotoReports, id)
            : ApiRoutes.Format(ApiRoutes.CommentReports, ("id", id), ("commentId", commentId)), JsonContent.Create(request, options: Json), ct);

    // ----- Blogs -----
    public Task<PagedResult<BlogPostSummaryDto>> GetPostsAsync(int page, bool following = false, CancellationToken ct = default) =>
        GetAsync<PagedResult<BlogPostSummaryDto>>($"{ApiRoutes.BlogPosts}?page={page}" + (following ? "&following=true" : ""), ct);

    public Task<BlogPostDto> GetPostAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<BlogPostDto>(WithId(ApiRoutes.BlogPost, id), ct);

    public Task<List<BlogCommentDto>> GetBlogCommentsAsync(Guid postId, CancellationToken ct = default) =>
        GetAsync<List<BlogCommentDto>>(WithId(ApiRoutes.BlogPostComments, postId), ct);

    public Task<BlogCommentDto> AddBlogCommentAsync(Guid postId, string text, CancellationToken ct = default) =>
        SendAsync<BlogCommentDto>(HttpMethod.Post, WithId(ApiRoutes.BlogPostComments, postId),
            JsonContent.Create(new CreateBlogCommentRequest(text), options: Json), ct);

    public Task DeleteBlogCommentAsync(Guid postId, Guid commentId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, ApiRoutes.Format(ApiRoutes.BlogPostComment, ("id", postId), ("commentId", commentId)), null, ct);

    public Task ReportPostAsync(Guid postId, Guid? commentId, CreateReportRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, commentId is null
            ? WithId(ApiRoutes.BlogPostReports, postId)
            : ApiRoutes.Format(ApiRoutes.BlogCommentReports, ("id", postId), ("commentId", commentId)), JsonContent.Create(request, options: Json), ct);

    // ----- Collections -----
    public Task<List<CollectionSummaryDto>> GetCollectionsAsync(Guid? ownerId = null, CancellationToken ct = default) =>
        GetAsync<List<CollectionSummaryDto>>(ApiRoutes.Collections + (ownerId is null ? "" : $"?ownerId={ownerId}"), ct);

    public Task<CollectionDto> GetCollectionAsync(Guid id, int page, CancellationToken ct = default) =>
        GetAsync<CollectionDto>($"{WithId(ApiRoutes.Collection, id)}?page={page}", ct);

    public Task<CollectionSummaryDto> CreateCollectionAsync(SaveCollectionRequest request, CancellationToken ct = default) =>
        SendAsync<CollectionSummaryDto>(HttpMethod.Post, ApiRoutes.Collections, JsonContent.Create(request, options: Json), ct);

    public Task DeleteCollectionAsync(Guid id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, WithId(ApiRoutes.Collection, id), null, ct);

    public Task SetCollectionPhotoAsync(Guid id, Guid photoId, bool inCollection, CancellationToken ct = default) =>
        SendAsync(inCollection ? HttpMethod.Put : HttpMethod.Delete,
            ApiRoutes.Format(ApiRoutes.CollectionPhoto, ("id", id), ("photoId", photoId)), null, ct);

    public Task<List<CollectionChoiceDto>> GetCollectionChoicesAsync(Guid photoId, CancellationToken ct = default) =>
        GetAsync<List<CollectionChoiceDto>>(WithId(ApiRoutes.PhotoCollections, photoId), ct);

    // ----- Activity -----
    public Task<PagedResult<ActivityDto>> GetActivityAsync(int page, CancellationToken ct = default) =>
        GetAsync<PagedResult<ActivityDto>>($"{ApiRoutes.Activity}?page={page}", ct);

    public Task<UnreadActivityDto> GetUnreadActivityAsync(CancellationToken ct = default) =>
        GetAsync<UnreadActivityDto>(ApiRoutes.ActivityUnread, ct);

    public Task MarkActivityReadAsync(CancellationToken ct = default) => SendAsync(HttpMethod.Post, ApiRoutes.ActivityRead, null, ct);

    public Task<ActivitySettingsDto> GetActivitySettingsAsync(CancellationToken ct = default) =>
        GetAsync<ActivitySettingsDto>(ApiRoutes.ActivitySettings, ct);

    public Task<ActivitySettingsDto> SaveActivitySettingsAsync(ActivitySettingsDto settings, CancellationToken ct = default) =>
        SendAsync<ActivitySettingsDto>(HttpMethod.Put, ApiRoutes.ActivitySettings, JsonContent.Create(settings, options: Json), ct);

    // ----- Photo challenges -----
    public Task<List<ChallengeSummaryDto>> GetChallengesAsync(CancellationToken ct = default) =>
        GetAsync<List<ChallengeSummaryDto>>(ApiRoutes.Challenges, ct);

    public Task<ChallengeDto> GetChallengeAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<ChallengeDto>(WithId(ApiRoutes.Challenge, id), ct);

    public Task EnterChallengeAsync(Guid id, Guid photoId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, WithId(ApiRoutes.ChallengeEntries, id), JsonContent.Create(new EnterChallengeRequest(photoId), options: Json), ct);

    public Task WithdrawChallengeEntryAsync(Guid id, Guid photoId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, ApiRoutes.Format(ApiRoutes.ChallengeEntry, ("id", id), ("photoId", photoId)), null, ct);

    // ----- Places -----
    public Task<List<PlaceDto>> GetPlacesAsync(CancellationToken ct = default) => GetAsync<List<PlaceDto>>(ApiRoutes.Places, ct);

    public Task<PlaceDto> CreatePlaceAsync(SavePlaceRequest request, CancellationToken ct = default) =>
        SendAsync<PlaceDto>(HttpMethod.Post, ApiRoutes.Places, JsonContent.Create(request, options: Json), ct);

    public Task<PlaceDto> UpdatePlaceAsync(Guid id, SavePlaceRequest request, CancellationToken ct = default) =>
        SendAsync<PlaceDto>(HttpMethod.Put, WithId(ApiRoutes.Place, id), JsonContent.Create(request, options: Json), ct);

    public Task DeletePlaceAsync(Guid id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, WithId(ApiRoutes.Place, id), null, ct);

    // ----- Email notifications -----
    public Task<EmailNotificationsDto> GetNotificationsAsync(CancellationToken ct = default) =>
        GetAsync<EmailNotificationsDto>(ApiRoutes.MyNotifications, ct);

    public Task<EmailNotificationsDto> SaveNotificationsAsync(SaveEmailNotificationsRequest request, CancellationToken ct = default) =>
        SendAsync<EmailNotificationsDto>(HttpMethod.Put, ApiRoutes.MyNotifications, JsonContent.Create(request, options: Json), ct);

    public Task ResendNotificationConfirmationAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, ApiRoutes.MyNotificationsConfirmation, null, ct);

    // ----- Plumbing -----
    private static string WithId(string template, Guid id) => ApiRoutes.Format(template, ("id", id));

    private Task<T> GetAsync<T>(string url, CancellationToken ct) => SendAsync<T>(HttpMethod.Get, url, null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string url, HttpContent? body, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, url, body, ct);
        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    private async Task SendAsync(HttpMethod method, string url, HttpContent? body, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, url, body, ct);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string url, HttpContent? body, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(method, url) { Content = body };
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new ApiException("Can't reach PhotoWorld. Check your connection and try again.");
        }
        if (response.IsSuccessStatusCode) return response;

        using (response)
        {
            throw new ApiException(response.StatusCode switch
            {
                HttpStatusCode.BadRequest => await ReadProblemAsync(response, ct),
                HttpStatusCode.Unauthorized => "Your session has ended. Please sign in again.",
                HttpStatusCode.Forbidden or HttpStatusCode.NotFound => "That's no longer available.",
                HttpStatusCode.Conflict => "You've already reported this.",
                HttpStatusCode.RequestEntityTooLarge => "That photo is too large.",
                HttpStatusCode.BadGateway => await ReadProblemAsync(response, ct),
                _ => "Something went wrong. Please try again.",
            }, response.StatusCode);
        }
    }

    /// <summary>The first validation message from an ASP.NET Core problem response.</summary>
    private static async Task<string> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<Problem>(Json, ct);
            return problem?.Errors?.Values.SelectMany(m => m).FirstOrDefault() ?? problem?.Detail ?? "That wasn't valid.";
        }
        catch (JsonException)
        {
            return "That wasn't valid.";
        }
    }

    private sealed record Problem(string? Detail, Dictionary<string, string[]>? Errors);
}

public sealed class ApiException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>Adds the member's token to API calls, and ends the session when the API no longer accepts it.</summary>
public sealed class ApiAuthHandler(AuthService auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (auth.AccessToken is { } token) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await base.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && auth.IsSignedIn) MainThread.BeginInvokeOnMainThread(auth.SignOut);
        return response;
    }
}
