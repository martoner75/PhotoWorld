using System.Text;
using PhotoWorld.Shared.Blogs;

namespace PhotoWorld.Api.Services.Blogs;

/// <summary>Rules for PDFs attached to blog posts.</summary>
public static class BlogAttachments
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    /// <summary>True if the bytes start like a PDF. The browser's content type and file extension aren't trusted.</summary>
    public static bool LooksLikePdf(ReadOnlySpan<byte> content) => content.StartsWith(PdfSignature);

    /// <summary>
    /// The name readers download the file as: no folders, control or reserved characters, at most
    /// <see cref="BlogLimits.AttachmentFileNameMax"/> characters, always ending in ".pdf".
    /// </summary>
    public static string CleanFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        var builder = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormC))
            builder.Append(char.IsControl(c) || "/\\:*?\"<>|".Contains(c) ? '_' : c);
        name = builder.ToString().Trim();

        if (name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        name = name.Trim().Trim('.');
        if (name.Length == 0) name = "document";
        const int maxStem = BlogLimits.AttachmentFileNameMax - 4;
        if (name.Length > maxStem) name = name[..(char.IsHighSurrogate(name[maxStem - 1]) ? maxStem - 1 : maxStem)].TrimEnd();
        return name + ".pdf";
    }

    public static string StoragePath(Guid authorId, Guid postId, Guid attachmentId) => $"{authorId}/{postId}/{attachmentId}.pdf";
}
