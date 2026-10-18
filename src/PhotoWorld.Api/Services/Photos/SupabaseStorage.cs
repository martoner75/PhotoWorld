using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace PhotoWorld.Api.Services.Photos;

public sealed class SupabaseStorageOptions
{
    public const string Section = "Supabase";

    /// <summary>Project URL, e.g. https://&lt;project-ref&gt;.supabase.co</summary>
    public string Url { get; set; } = "";

    /// <summary>Secret key (sb_secret_…) or legacy service_role key. Server-side only.</summary>
    public string SecretKey { get; set; } = "";

    public string PhotoBucket { get; set; } = "photos";

    /// <summary>Public bucket for PDFs attached to blog posts.</summary>
    public string AttachmentBucket { get; set; } = "attachments";
}

/// <summary>
/// Minimal client for the Supabase Storage REST API: upload, delete and public URLs. Photos by default;
/// pass <see cref="SupabaseStorageOptions.AttachmentBucket"/> (via <see cref="AttachmentBucket"/>) for blog PDFs.
/// </summary>
public sealed class SupabaseStorage(HttpClient http, IOptions<SupabaseStorageOptions> options)
{
    private readonly SupabaseStorageOptions _options = options.Value;

    public string AttachmentBucket => _options.AttachmentBucket;

    public string PublicUrl(string path, string? bucket = null) =>
        $"{_options.Url.TrimEnd('/')}/storage/v1/object/public/{bucket ?? _options.PhotoBucket}/{path}";

    /// <summary>A public URL that makes browsers save the file as <paramref name="fileName"/> instead of opening it.</summary>
    public string DownloadUrl(string path, string fileName, string? bucket = null) =>
        $"{PublicUrl(path, bucket)}?download={Uri.EscapeDataString(fileName)}";

    public async Task UploadAsync(string path, byte[] content, string contentType, CancellationToken ct, string? bucket = null)
    {
        using var request = CreateRequest(HttpMethod.Post, $"storage/v1/object/{bucket ?? _options.PhotoBucket}/{path}");
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        // Each path holds one file forever (new uploads get new ids), so browsers and the CDN can cache it for a year.
        request.Headers.Add("cache-control", "max-age=31536000");
        request.Headers.Add("x-upsert", "true");

        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "upload", path, ct);
    }

    public async Task DeleteAsync(IEnumerable<string> paths, CancellationToken ct, string? bucket = null)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"storage/v1/object/{bucket ?? _options.PhotoBucket}");
        request.Content = JsonContent.Create(new { prefixes = paths.ToArray() });

        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "delete", string.Join(", ", paths), ct);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_options.Url.TrimEnd('/') + "/"), relativeUrl));
        request.Headers.Add("apikey", _options.SecretKey);
        // Legacy service_role keys are JWTs and go in Authorization too; new sb_secret_ keys must not.
        if (!_options.SecretKey.StartsWith("sb_", StringComparison.Ordinal))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action, string path, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"Supabase Storage {action} failed for '{path}': {(int)response.StatusCode} {body}", null, response.StatusCode);
    }
}
