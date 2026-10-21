using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Ganss.Xss;
using PhotoWorld.Shared.Blogs;

namespace PhotoWorld.Api.Services.Blogs;

public sealed record CleanedPost(string Html, IReadOnlyList<Guid> PhotoIds, string Excerpt, bool IsEmpty);

/// <summary>
/// Everything that touches blog post HTML. Posts come from a rich-text editor in the browser, so the API
/// never trusts them: only basic formatting survives, and the only images allowed are the author's own
/// PhotoWorld photos, embedded as <c>&lt;figure class="pw-photo" data-photo-id="…"&gt;</c>.
/// </summary>
public sealed partial class BlogHtml
{
    public const string PhotoClass = "pw-photo";
    private const string PhotoIdAttribute = "data-photo-id";

    private readonly HtmlSanitizer _sanitizer;
    private readonly HtmlParser _parser = new();

    public BlogHtml()
    {
        _sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "br", "h2", "h3", "strong", "em", "u", "s", "a", "ul", "ol", "li", "blockquote",
                "figure", "figcaption", "img", "span",
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "href", "src", "alt", "class", "style", PhotoIdAttribute,
            },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "src" },
            // Text colour from the editor (<span style="color: …">); only plain colour values are kept.
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "color" },
        });
        // With "class" allowed, only this class survives (keeps the photo embeds recognisable).
        _sanitizer.AllowedClasses.Add(PhotoClass);
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IHtmlAnchorElement link)
            {
                link.SetAttribute("rel", "nofollow noopener noreferrer");
                link.SetAttribute("target", "_blank");
            }
            if (e.Node is IElement element && element.GetAttribute("style") is { } style && !ColorStyle().IsMatch(style))
                element.RemoveAttribute("style");
        };
    }

    /// <summary>Photo ids the raw (unsanitized) HTML refers to, so the caller can look up which are the author's.</summary>
    public IReadOnlySet<Guid> ReferencedPhotoIds(string html)
    {
        var body = Parse(html);
        return body.QuerySelectorAll($"[{PhotoIdAttribute}]")
            .Select(e => Guid.TryParse(e.GetAttribute(PhotoIdAttribute), out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
    }

    /// <summary>
    /// Sanitizes a post body. Embedded photos are kept only when they're in <paramref name="ownPhotoUrls"/>
    /// (the author's photos → display URL), and their image source is rewritten to that URL.
    /// </summary>
    public CleanedPost Clean(string html, IReadOnlyDictionary<Guid, string> ownPhotoUrls)
    {
        var body = Parse(_sanitizer.Sanitize(html));
        var photoIds = new List<Guid>();

        foreach (var img in body.QuerySelectorAll("img").ToList())
        {
            var figure = img.Closest("figure");
            var rawId = img.GetAttribute(PhotoIdAttribute) ?? figure?.GetAttribute(PhotoIdAttribute);
            if (!Guid.TryParse(rawId, out var id) || !ownPhotoUrls.TryGetValue(id, out var url))
            {
                img.Remove();
                continue;
            }

            // Normalise to <figure class="pw-photo" data-photo-id><img src alt><figcaption?></figure>.
            if (figure is null)
            {
                figure = body.Owner!.CreateElement("figure");
                img.Parent!.InsertBefore(figure, img);
                figure.AppendChild(img);
            }
            figure.ClassName = PhotoClass;
            figure.SetAttribute(PhotoIdAttribute, id.ToString());
            img.RemoveAttribute(PhotoIdAttribute);
            img.RemoveAttribute("class");
            img.SetAttribute("src", url);
            img.SetAttribute("alt", img.GetAttribute("alt") ?? "");
            if (!photoIds.Contains(id)) photoIds.Add(id);
        }
        foreach (var figure in body.QuerySelectorAll("figure").Where(f => f.QuerySelector("img") is null).ToList())
            figure.Remove();

        var text = PlainText(body);
        return new CleanedPost(body.InnerHtml, photoIds, Excerpt(text), IsEmpty: text.Length == 0 && photoIds.Count == 0);
    }

    /// <summary>Drops embedded photos the reader may not see any more (deleted, or hidden by moderation).</summary>
    public string WithoutPhotosExcept(string html, IReadOnlySet<Guid> visiblePhotoIds)
    {
        var body = Parse(html);
        foreach (var figure in body.QuerySelectorAll($"figure[{PhotoIdAttribute}]").ToList())
        {
            if (!Guid.TryParse(figure.GetAttribute(PhotoIdAttribute), out var id) || !visiblePhotoIds.Contains(id))
                figure.Remove();
        }
        return body.InnerHtml;
    }

    private IElement Parse(string html) => _parser.ParseDocument($"<!doctype html><body>{html}</body>").Body!;

    private static string PlainText(IElement body)
    {
        // Text of each block, without figure captions, so excerpts read like the post's prose.
        var blocks = body.QuerySelectorAll("p, h2, h3, li, blockquote")
            .Where(e => e.Closest("figure") is null)
            .Select(e => e.TextContent);
        return Whitespace().Replace(string.Join(" ", blocks), " ").Trim();
    }

    internal static string Excerpt(string text)
    {
        if (text.Length <= BlogLimits.ExcerptLength) return text;
        var cut = text.LastIndexOf(' ', BlogLimits.ExcerptLength);
        return text[..(cut > BlogLimits.ExcerptLength / 2 ? cut : BlogLimits.ExcerptLength)].TrimEnd(',', '.', ';', ':') + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // A single colour as a hex code or rgb()/rgba() numbers, e.g. "color: rgba(255, 138, 128, 1)".
    [GeneratedRegex(@"^\s*color:\s*(#[0-9a-f]{3,8}|rgba?\(\s*[\d.]+%?(\s*,\s*[\d.]+%?){2,3}\s*\))\s*;?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ColorStyle();
}
