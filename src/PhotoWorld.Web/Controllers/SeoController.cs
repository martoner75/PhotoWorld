using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotoWorld.Shared.Portfolios;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

/// <summary>
/// robots.txt and sitemap.xml for search engines. The site is members-only, so the sitemap lists just the pages
/// anyone can open: the home page, the guide, the privacy policy and terms, and every public portfolio.
/// Crawlers only read these at the root of a domain, so they work when the website is served from the site root.
/// </summary>
[AllowAnonymous]
public class SeoController(IPhotoWorldApiClient api, IMemoryCache cache, ILogger<SeoController> logger) : Controller
{
    private const string PortfolioCacheKey = "sitemap-portfolios";
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    [HttpGet("/robots.txt")]
    public ContentResult Robots()
    {
        // Links in these areas carry personal tokens (confirm / unsubscribe) or need a sign-in anyway.
        var text = new StringBuilder()
            .AppendLine("User-agent: *")
            .AppendLine("Disallow: /Account/")
            .AppendLine("Disallow: /Admin/")
            .AppendLine("Disallow: /Notifications/")
            .AppendLine()
            .AppendLine($"Sitemap: {Absolute(Url.Action(nameof(Sitemap), "Seo"))}")
            .ToString();
        return Content(text, "text/plain", Encoding.UTF8);
    }

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var portfolios = await cache.GetOrCreateAsync(PortfolioCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            try
            {
                return await api.GetPortfolioIndexAsync(ct);
            }
            catch (HttpRequestException ex)
            {
                // The fixed pages are still worth listing if the API is down; try the portfolios again soon.
                logger.LogWarning(ex, "Could not load the public portfolios for the sitemap");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return (IReadOnlyList<PortfolioIndexEntryDto>)[];
            }
        }) ?? [];

        var urlset = new XElement(Ns + "urlset");
        void Add(string? path, DateTimeOffset? lastModified = null)
        {
            var url = new XElement(Ns + "url", new XElement(Ns + "loc", Absolute(path)));
            if (lastModified is { } modified)
                url.Add(new XElement(Ns + "lastmod", modified.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            urlset.Add(url);
        }

        Add(Url.Action("Index", "Home"));
        Add(Url.Action("Guide", "Home"));
        Add(Url.Action("Privacy", "Home"));
        Add(Url.Action("Terms", "Home"));
        foreach (var p in portfolios) Add(Url.Action("Show", "Portfolio", new { slug = p.Slug }), p.UpdatedAt);

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
        return Content($"{document.Declaration}{Environment.NewLine}{document}", "application/xml", Encoding.UTF8);
    }

    /// <summary>Search engines need full addresses; <see cref="IUrlHelper"/> gives paths that already include any app path.</summary>
    private string Absolute(string? path) => $"{Request.Scheme}://{Request.Host}{path}";
}
