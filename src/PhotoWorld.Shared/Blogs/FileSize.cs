using System.Globalization;

namespace PhotoWorld.Shared.Blogs;

public static class FileSize
{
    /// <summary>A human-readable size such as "840 KB" or "3.2 MB".</summary>
    public static string Format(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{Math.Ceiling(bytes / 1024.0).ToString(CultureInfo.InvariantCulture)} KB",
        _ => $"{(bytes / (1024.0 * 1024)).ToString("0.#", CultureInfo.InvariantCulture)} MB",
    };
}
