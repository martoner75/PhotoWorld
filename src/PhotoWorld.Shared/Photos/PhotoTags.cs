using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PhotoWorld.Shared.Photos;

/// <summary>
/// Photo tags like #fog or #street_photography: lower case letters (any language), digits, '_' and '-', stored
/// without the '#'. Owners type them in a tags field, and #hashtags in the description count too.
/// </summary>
public static partial class PhotoTags
{
    public const int MaxLength = 40;
    public const int MaxPerPhoto = 10;

    /// <summary>A tag in its stored form, or null if nothing usable is left ("#Fog!" → "fog").</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim().TrimStart('#').Normalize(NormalizationForm.FormC).ToLower(CultureInfo.InvariantCulture);
        var kept = new StringBuilder(text.Length);
        foreach (var c in text)
            if (char.IsLetterOrDigit(c) || c is '_' or '-') kept.Append(c);
        var tag = kept.ToString().Trim('-', '_');
        if (tag.Length == 0) return null;
        return tag.Length <= MaxLength ? tag : tag[..MaxLength];
    }

    /// <summary>
    /// Tags from the tags field (separated by spaces, commas or '#') plus #hashtags in the description, without
    /// duplicates, in the order written, at most <see cref="MaxPerPhoto"/>.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? tagsField, string? description = null)
    {
        var words = (tagsField ?? "").Split([' ', ',', ';', '#', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Concat(Hashtag().Matches(description ?? "").Select(m => m.Groups[1].Value));
        return words.Select(Normalize).OfType<string>().Distinct().Take(MaxPerPhoto).ToList();
    }

    /// <summary>The tags as an owner would type them back in: "fog wildlife london".</summary>
    public static string Join(IEnumerable<string>? tags) => string.Join(' ', tags ?? []);

    [GeneratedRegex(@"(?<![\w#/&])#([\p{L}\p{N}_-]{1,40})")]
    private static partial Regex Hashtag();
}
