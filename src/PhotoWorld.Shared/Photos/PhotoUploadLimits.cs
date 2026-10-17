namespace PhotoWorld.Shared.Photos;

/// <summary>Upload rules, shared so the UI can validate early and the API enforces the same limits.</summary>
public static class PhotoUploadLimits
{
    public const long MaxBytes = 25 * 1024 * 1024;
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 2000;
    public const int MaxCommentLength = 1000;

    public static readonly IReadOnlyList<string> AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Value for an <c>&lt;input type="file" accept="…"&gt;</c> attribute.</summary>
    public static string AcceptAttribute => string.Join(",", AllowedContentTypes);
}
