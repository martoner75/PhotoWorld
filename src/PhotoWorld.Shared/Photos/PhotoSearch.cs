using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace PhotoWorld.Shared.Photos;

/// <summary>
/// Gallery filters: words, a tag, and the camera settings read from each photo's EXIF
/// data. Every field is optional; photos that didn't record a setting never match a filter on it. Text matches anywhere
/// in the value, ignoring case.
/// </summary>
public sealed class PhotoSearch
{
    public const int TextMax = 100;

    /// <summary>Words in the title or description, or a tag (searching "fog" also finds #fog).</summary>
    [StringLength(TextMax)] public string? Text { get; set; }

    /// <summary>Only photos with this tag (with or without '#').</summary>
    [StringLength(PhotoTags.MaxLength + 1)] public string? Tag { get; set; }

    /// <summary>Matches the camera's make or model, e.g. "sony" or "X-T5".</summary>
    [StringLength(TextMax)] public string? Camera { get; set; }
    [StringLength(TextMax)] public string? Lens { get; set; }

    [Range(1, 5000)] public decimal? FocalMin { get; set; }
    [Range(1, 5000)] public decimal? FocalMax { get; set; }

    /// <summary>ƒ-numbers: a lower number is a wider aperture.</summary>
    [Range(0.5, 128)] public decimal? ApertureMin { get; set; }
    [Range(0.5, 128)] public decimal? ApertureMax { get; set; }

    [Range(1, 10_000_000)] public int? IsoMin { get; set; }
    [Range(1, 10_000_000)] public int? IsoMax { get; set; }

    public bool IsEmpty => !QueryValues().Any();

    /// <summary>The filters that are set, as query-string name/value pairs (names match the properties).</summary>
    public IEnumerable<(string Name, string Value)> QueryValues()
    {
        if (!string.IsNullOrWhiteSpace(Text)) yield return ("text", Text.Trim());
        if (PhotoTags.Normalize(Tag) is { } tag) yield return ("tag", tag);
        if (!string.IsNullOrWhiteSpace(Camera)) yield return ("camera", Camera.Trim());
        if (!string.IsNullOrWhiteSpace(Lens)) yield return ("lens", Lens.Trim());
        if (FocalMin is { } focalMin) yield return ("focalMin", Format(focalMin));
        if (FocalMax is { } focalMax) yield return ("focalMax", Format(focalMax));
        if (ApertureMin is { } apertureMin) yield return ("apertureMin", Format(apertureMin));
        if (ApertureMax is { } apertureMax) yield return ("apertureMax", Format(apertureMax));
        if (IsoMin is { } isoMin) yield return ("isoMin", isoMin.ToString(CultureInfo.InvariantCulture));
        if (IsoMax is { } isoMax) yield return ("isoMax", isoMax.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The filters that are set, in words: "Sony", "85mm", "16–35 mm", "ƒ/1.4–2.8", "ISO from 800".</summary>
    public IEnumerable<string> Describe()
    {
        if (!string.IsNullOrWhiteSpace(Text)) yield return $"“{Text.Trim()}”";
        if (PhotoTags.Normalize(Tag) is { } tag) yield return "#" + tag;
        if (!string.IsNullOrWhiteSpace(Camera)) yield return Camera.Trim();
        if (!string.IsNullOrWhiteSpace(Lens)) yield return Lens.Trim();
        if (Range(FocalMin, FocalMax) is { } focal) yield return focal + " mm";
        if (Range(ApertureMin, ApertureMax) is { } aperture) yield return "ƒ/" + aperture;
        if (Range(IsoMin, IsoMax) is { } iso) yield return "ISO " + iso;
    }

    private static string? Range<T>(T? min, T? max) where T : struct, IFormattable =>
        (min, max) switch
        {
            (null, null) => null,
            ({ } a, null) => $"from {Show(a)}",
            (null, { } b) => $"up to {Show(b)}",
            ({ } a, { } b) when Show(a) == Show(b) => Show(a),
            ({ } a, { } b) => $"{Show(a)}–{Show(b)}",
        };

    private static string Show(IFormattable value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>Cameras and lenses that appear in the gallery, most used first, to suggest in the filter form.</summary>
public sealed record CameraOptionsDto(IReadOnlyList<string> Cameras, IReadOnlyList<string> Lenses);
