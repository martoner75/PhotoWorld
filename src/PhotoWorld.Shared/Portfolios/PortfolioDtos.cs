using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Shared.Portfolios;

public static class PortfolioLimits
{
    public const int SlugMin = 3;
    public const int SlugMax = 40;
    public const int BioMax = 500;
    public const int LinkMax = 200;
    public const int PhotosMax = 24;
    /// <summary>How many of the owner's photos the settings page offers to choose from (newest first).</summary>
    public const int CandidatesMax = 120;
}

/// <summary>Checks and clean-ups for portfolio settings, shared by the API (which enforces them) and the website.</summary>
public static partial class PortfolioRules
{
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9]|-(?=[a-z0-9]))*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[A-Za-z0-9._]{1,30}$")]
    private static partial Regex InstagramPattern();

    /// <summary>Lower case letters, digits and single hyphens, 3–40 characters: the part after /p/ in the address.</summary>
    public static bool IsValidSlug(string? slug) =>
        slug is { Length: >= PortfolioLimits.SlugMin and <= PortfolioLimits.SlugMax } && SlugPattern().IsMatch(slug);

    /// <summary>"Ana Silva-Löw" → "ana-silva-low": a starting suggestion for the address.</summary>
    public static string SuggestSlug(string displayName)
    {
        var plain = new StringBuilder();
        foreach (var c in displayName.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            plain.Append(char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        }
        var slug = string.Join('-', plain.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length > PortfolioLimits.SlugMax) slug = slug[..PortfolioLimits.SlugMax].TrimEnd('-');
        return IsValidSlug(slug) ? slug : "photographer";
    }

    /// <summary>"@ana.silva", "instagram.com/ana.silva/" or "ana.silva" → "ana.silva"; null if empty or not a handle.</summary>
    public static string? InstagramHandle(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var handle = input.Trim();
        var marker = handle.IndexOf("instagram.com/", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0) handle = handle[(marker + "instagram.com/".Length)..];
        handle = handle.TrimStart('@').Split('/', '?')[0];
        return InstagramPattern().IsMatch(handle) ? handle : null;
    }

    /// <summary>An absolute http(s) address, or null.</summary>
    public static string? Website(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var text = input.Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.Host.Contains('.')
            ? uri.ToString() : null;
    }
}

/// <summary>A photographer's public portfolio, as anyone (signed in or not) sees it at /p/{slug}.</summary>
public sealed record PublicPortfolioDto(
    string Slug,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    string? Website,
    string? Instagram,
    string? ContactEmail,
    IReadOnlyList<PortfolioPhotoDto> Photos);

public sealed record PortfolioPhotoDto(
    Guid Id,
    string Title,
    string? Description,
    string ImageUrl,
    string ThumbnailUrl,
    int Width,
    int Height,
    bool AllowDownload);

/// <summary>
/// The owner's settings. <see cref="PhotoIds"/> are the chosen photos in order; <see cref="Candidates"/> are the
/// owner's photos that may be shown publicly (approved and not hidden), newest first.
/// </summary>
public sealed record MyPortfolioDto(
    bool IsPublic,
    string Slug,
    string? Bio,
    string? Website,
    string? Instagram,
    string? ContactEmail,
    IReadOnlyList<Guid> PhotoIds,
    IReadOnlyList<PhotoSummaryDto> Candidates);

public sealed record SavePortfolioRequest(
    bool IsPublic,
    [Required, StringLength(PortfolioLimits.SlugMax)] string Slug,
    [StringLength(PortfolioLimits.BioMax)] string? Bio,
    [StringLength(PortfolioLimits.LinkMax)] string? Website,
    [StringLength(PortfolioLimits.LinkMax)] string? Instagram,
    [StringLength(PortfolioLimits.LinkMax), EmailAddress] string? ContactEmail,
    IReadOnlyList<Guid> PhotoIds);
