using System.Globalization;
using System.Text;
using ImageMagick;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PhotoWorld.Shared.Photos;
using SkiaSharp;

namespace PhotoWorld.Api.Services.Photos;

/// <summary>Thrown when an upload isn't an image PhotoWorld accepts. The message is safe to show to the user.</summary>
public sealed class ImageValidationException(string message) : Exception(message);

public sealed record EncodedImage(byte[] Bytes, int Width, int Height);

public sealed record ProcessedPhoto(EncodedImage Display, EncodedImage Thumbnail, CameraDetailsDto Camera);

/// <summary>
/// Turns an uploaded file into the two JPEGs PhotoWorld stores. Re-encoding drops every bit of metadata
/// (including GPS), which matters because the storage bucket is public. Camera settings are read first
/// and returned separately.
/// </summary>
public sealed class ImageProcessor
{
    public const int DisplayMaxSide = 2560;
    public const int ThumbnailMaxSide = 640;
    private const int DisplayQuality = 88;
    private const int ThumbnailQuality = 80;
    private const long MaxPixels = 120_000_000;
    private const string TooManyPixels = "That photo has too many pixels (the limit is 120 megapixels).";

    // ISO base media "ftyp" brands used by HEIC/HEIF stills (iPhone photos are "heic").
    private static readonly string[] HeifBrands = ["heic", "heix", "hevc", "hevx", "heim", "heis", "mif1", "msf1"];

    // Open Sans (open font licence; the same file the app ships), embedded: the Linux build of Skia has no system fonts.
    private static readonly Lazy<SKTypeface> WatermarkFont = new(() =>
        SKTypeface.FromStream(typeof(ImageProcessor).Assembly.GetManifestResourceStream("watermark-font.ttf")!));

    /// <param name="watermark">Text drawn in the bottom-right corner of the display image (not the thumbnail), or null.</param>
    public ProcessedPhoto Process(byte[] file, string? watermark = null)
    {
        if (file.Length == 0) throw new ImageValidationException("The file is empty.");
        if (file.Length > PhotoUploadLimits.MaxBytes)
            throw new ImageValidationException($"Photos can be up to {PhotoUploadLimits.MaxBytes / (1024 * 1024)} MB.");

        // Camera details come from the original file, before any conversion drops them.
        var camera = ReadCamera(file);

        // Skia can't decode HEIC (iPhone photos), so those are converted to an upright, metadata-free JPEG first.
        if (IsHeif(file)) file = ConvertHeifToJpeg(file);

        // Trust the file's actual content, not the browser-supplied content type or extension.
        using var codec = SKCodec.Create(new MemoryStream(file))
            ?? throw new ImageValidationException("That file isn't an image we can read. Use JPEG, PNG, WebP or HEIC.");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            throw new ImageValidationException("Only JPEG, PNG, WebP and HEIC photos are supported.");
        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
            throw new ImageValidationException(TooManyPixels);

        using var decoded = DecodeForDisplay(codec);
        using var oriented = ApplyOrientation(decoded, codec.EncodedOrigin);
        var thumbnail = EncodeJpeg(oriented, ThumbnailMaxSide, ThumbnailQuality);
        using var display = Resize(oriented, DisplayMaxSide);
        if (!string.IsNullOrWhiteSpace(watermark)) DrawWatermark(display, watermark.Trim());
        return new ProcessedPhoto(Encode(display, DisplayQuality), thumbnail, camera);
    }

    /// <summary>
    /// Light text with a soft shadow, sized to the image (about 2.4% of the long side) so it reads on bright and dark
    /// photos alike without covering the subject.
    /// </summary>
    internal static void DrawWatermark(SKBitmap bitmap, string text)
    {
        var size = Math.Max(12f, Math.Max(bitmap.Width, bitmap.Height) * 0.024f);
        var margin = size * 0.9f;
        // No hinting and linear metrics: what's measured is what's drawn, on macOS and Linux alike.
        using var font = new SKFont(WatermarkFont.Value, size)
        {
            Hinting = SKFontHinting.None, Subpixel = true, LinearMetrics = true, Edging = SKFontEdging.Antialias,
        };
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, 190) };
        paint.ImageFilter = SKImageFilter.CreateDropShadow(0, size * 0.06f, size * 0.12f, size * 0.12f, new SKColor(0, 0, 0, 150));

        // Never wider than half the photo: long names shrink (down to 10px, still readable), then are shortened with "…".
        var limit = bitmap.Width / 2f;
        float Width(string s) { font.MeasureText(s, out var bounds); return bounds.Right; }
        if (Width(text) > limit) font.Size = Math.Max(10f, font.Size * limit / Width(text));
        while (text.Length > 2 && Width(text) > limit) text = text[..^(text.EndsWith('…') ? 2 : 1)].TrimEnd() + "…";

        using var canvas = new SKCanvas(bitmap);
        canvas.DrawText(text, bitmap.Width - margin - Width(text), bitmap.Height - margin, SKTextAlign.Left, font, paint);
    }

    internal static bool IsHeif(byte[] file) =>
        file.Length >= 12
        && Encoding.ASCII.GetString(file, 4, 4) == "ftyp"
        && HeifBrands.Contains(Encoding.ASCII.GetString(file, 8, 4));

    /// <summary>
    /// Decodes HEIC/HEIF with ImageMagick (libheif), applies its rotation and strips metadata, returning a
    /// high-quality JPEG for the normal Skia pipeline.
    /// </summary>
    private static byte[] ConvertHeifToJpeg(byte[] file)
    {
        try
        {
            var info = new MagickImageInfo(file);
            if ((long)info.Width * info.Height > MaxPixels) throw new ImageValidationException(TooManyPixels);

            using var image = new MagickImage(file);
            image.AutoOrient();
            image.Strip();
            image.Quality = 95;
            return image.ToByteArray(MagickFormat.Jpeg);
        }
        catch (MagickException)
        {
            throw new ImageValidationException("That HEIC photo couldn't be read. Try exporting it as JPEG.");
        }
    }

    /// <summary>Decodes at the smallest native scale that's still at least display size, to save memory on huge files.</summary>
    private static SKBitmap DecodeForDisplay(SKCodec codec)
    {
        var longest = Math.Max(codec.Info.Width, codec.Info.Height);
        var size = longest > DisplayMaxSide
            ? codec.GetScaledDimensions(DisplayMaxSide / (float)longest)
            : codec.Info.Size;
        if (Math.Max(size.Width, size.Height) < Math.Min(longest, DisplayMaxSide)) size = codec.Info.Size;

        var bitmap = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new ImageValidationException("That image looks damaged and couldn't be read.");
        }
        return bitmap;
    }

    /// <summary>
    /// Rotates/flips pixels so the image displays upright without its EXIF orientation tag, and flattens
    /// transparency onto white (JPEG has no alpha channel).
    /// </summary>
    internal static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int w = source.Width, h = source.Height;
        var target = new SKBitmap(new SKImageInfo(swap ? h : w, swap ? w : h, SKColorType.Rgba8888, SKAlphaType.Opaque));

        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.White);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: canvas.Translate(w, 0); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight: canvas.Translate(w, h); canvas.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: canvas.Translate(0, h); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(h, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: canvas.Translate(h, w); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, w); canvas.RotateDegrees(270); break;
        }
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
        return target;
    }

    private static EncodedImage EncodeJpeg(SKBitmap source, int maxSide, int quality)
    {
        using var resized = Resize(source, maxSide);
        return Encode(resized, quality);
    }

    private static SKBitmap Resize(SKBitmap source, int maxSide)
    {
        var scale = Math.Min(1f, maxSide / (float)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return scale < 1f
            ? source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            : source.Copy();
    }

    private static EncodedImage Encode(SKBitmap bitmap, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return new EncodedImage(data.ToArray(), bitmap.Width, bitmap.Height);
    }

    internal static CameraDetailsDto ReadCamera(byte[] file)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(new MemoryStream(file));
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            return new CameraDetailsDto(
                Make: Text(ifd0, ExifDirectoryBase.TagMake, 100),
                Model: Text(ifd0, ExifDirectoryBase.TagModel, 100),
                Lens: Text(exif, ExifDirectoryBase.TagLensModel, 150),
                FocalLengthMm: Rational(exif, ExifDirectoryBase.TagFocalLength) is { } f ? Math.Round((decimal)f, 1) : null,
                Aperture: Rational(exif, ExifDirectoryBase.TagFNumber) is { } a ? Math.Round((decimal)a, 1) : null,
                ExposureTime: Rational(exif, ExifDirectoryBase.TagExposureTime) is { } t and > 0 ? FormatExposure(t) : null,
                Iso: exif is not null && exif.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var iso) && iso > 0 ? iso : null,
                // Cameras record local wall-clock time; it's kept as-is (labelled UTC) and shown without conversion.
                TakenAt: exif is not null && exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var taken)
                    ? new DateTimeOffset(DateTime.SpecifyKind(taken, DateTimeKind.Unspecified), TimeSpan.Zero)
                    : null);
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException or MetadataException)
        {
            return new CameraDetailsDto(null, null, null, null, null, null, null, null);
        }
    }

    private static string? Text(MetadataExtractor.Directory? directory, int tag, int maxLength)
    {
        var value = directory?.GetString(tag)?.Trim().TrimEnd('\0');
        return string.IsNullOrWhiteSpace(value) ? null : value.Length > maxLength ? value[..maxLength] : value;
    }

    private static double? Rational(MetadataExtractor.Directory? directory, int tag) =>
        directory is not null && directory.TryGetRational(tag, out var r) && r.Denominator != 0 ? r.ToDouble() : null;

    internal static string FormatExposure(double seconds) => seconds >= 0.3
        ? seconds.ToString("0.#", CultureInfo.InvariantCulture) + "s"
        : "1/" + Math.Round(1 / seconds).ToString(CultureInfo.InvariantCulture);
}
