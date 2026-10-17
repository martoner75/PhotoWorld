using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared.Photos;
using SkiaSharp;

namespace PhotoWorld.Api.Tests;

public class ImageProcessorTests
{
    private readonly ImageProcessor _processor = new();

    [Fact]
    public void Resizes_large_photo_to_display_and_thumbnail_limits()
    {
        var result = _processor.Process(TestImages.Jpeg(4000, 3000));

        Assert.Equal((2560, 1920), (result.Display.Width, result.Display.Height));
        Assert.Equal((640, 480), (result.Thumbnail.Width, result.Thumbnail.Height));
        AssertJpegSize(result.Display.Bytes, 2560, 1920);
        AssertJpegSize(result.Thumbnail.Bytes, 640, 480);
    }

    [Fact]
    public void Does_not_upscale_small_photo()
    {
        var result = _processor.Process(TestImages.Jpeg(500, 300));

        Assert.Equal((500, 300), (result.Display.Width, result.Display.Height));
        Assert.Equal((500, 300), (result.Thumbnail.Width, result.Thumbnail.Height));
    }

    [Fact]
    public void Reads_camera_details_from_exif()
    {
        var camera = _processor.Process(TestImages.Jpeg(800, 600, TestImages.CameraExif())).Camera;

        Assert.Equal("Fujifilm", camera.Make);
        Assert.Equal("X-T5", camera.Model);
        Assert.Equal("XF35mmF1.4 R", camera.Lens);
        Assert.Equal(35m, camera.FocalLengthMm);
        Assert.Equal(2.8m, camera.Aperture);
        Assert.Equal("1/250", camera.ExposureTime);
        Assert.Equal(400, camera.Iso);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 18, 42, 7, TimeSpan.Zero), camera.TakenAt);
    }

    [Fact]
    public void Photo_without_exif_has_empty_camera_details()
    {
        Assert.True(_processor.Process(TestImages.Jpeg(400, 300)).Camera.IsEmpty);
    }

    [Fact]
    public void Stored_jpegs_contain_no_metadata_and_no_gps()
    {
        var original = TestImages.Jpeg(800, 600, TestImages.CameraExif(withGps: true));
        Assert.NotEmpty(ImageMetadataReader.ReadMetadata(new MemoryStream(original)).OfType<GpsDirectory>()); // sanity check

        var result = _processor.Process(original);

        foreach (var bytes in new[] { result.Display.Bytes, result.Thumbnail.Bytes })
        {
            var directories = ImageMetadataReader.ReadMetadata(new MemoryStream(bytes));
            Assert.Empty(directories.OfType<GpsDirectory>());
            Assert.Empty(directories.OfType<ExifIfd0Directory>());
            Assert.Empty(directories.OfType<ExifSubIfdDirectory>());
        }
    }

    [Fact]
    public void Applies_exif_orientation_so_photo_is_upright()
    {
        // Orientation 6: the camera was turned 90° clockwise; viewers must rotate the stored pixels to display it.
        var result = _processor.Process(TestImages.Jpeg(400, 200, TestImages.CameraExif(orientation: 6)));

        Assert.Equal((200, 400), (result.Display.Width, result.Display.Height));
        using var bitmap = SKBitmap.Decode(result.Display.Bytes);
        // The red marker that was top-left in the stored pixels ends up top-right once rotated.
        Assert.True(IsRed(bitmap.GetPixel(bitmap.Width - 10, 10)));
        Assert.False(IsRed(bitmap.GetPixel(10, 10)));
    }

    [Fact]
    public void Transparent_png_is_flattened_onto_white()
    {
        var result = _processor.Process(TestImages.Png(300, 200));

        using var bitmap = SKBitmap.Decode(result.Display.Bytes);
        var pixel = bitmap.GetPixel(150, 100);
        Assert.True(pixel.Red > 240 && pixel.Green > 240 && pixel.Blue > 240, $"Expected white, got {pixel}");
    }

    [Fact]
    public void Rejects_files_that_are_not_images()
    {
        var ex = Assert.Throws<ImageValidationException>(() => _processor.Process("not a photo"u8.ToArray()));
        Assert.Contains("JPEG, PNG, WebP or HEIC", ex.Message);
    }

    [Fact]
    public void Rejects_unsupported_image_formats()
    {
        Assert.Throws<ImageValidationException>(() => _processor.Process(TestImages.Gif()));
    }

    [Fact]
    public void Converts_iphone_heic_and_keeps_camera_details()
    {
        var heic = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "camera.heic"));
        Assert.True(ImageProcessor.IsHeif(heic));

        var result = _processor.Process(heic);

        // The fixture's pixels are 600x400 with a "rotated 90° clockwise" orientation, so it's stored upright as 400x600.
        AssertJpegSize(result.Display.Bytes, 400, 600);
        AssertJpegSize(result.Thumbnail.Bytes, 400, 600);
        using (var bitmap = SKBitmap.Decode(result.Display.Bytes))
        {
            Assert.True(IsRed(bitmap.GetPixel(bitmap.Width - 10, 10)), "Red marker should be top-right once upright");
            Assert.False(IsRed(bitmap.GetPixel(10, 10)));
        }
        Assert.Equal("X-T5", result.Camera.Model);
        Assert.Equal(2.8m, result.Camera.Aperture);
        var directories = ImageMetadataReader.ReadMetadata(new MemoryStream(result.Display.Bytes));
        Assert.Empty(directories.OfType<GpsDirectory>());
        Assert.Empty(directories.OfType<ExifIfd0Directory>());
    }

    [Fact]
    public void Jpeg_is_not_mistaken_for_heic()
    {
        Assert.False(ImageProcessor.IsHeif(TestImages.Jpeg(10, 10)));
    }

    [Fact]
    public void Damaged_heic_is_rejected_with_a_clear_message()
    {
        var heic = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "camera.heic"));
        var ex = Assert.Throws<ImageValidationException>(() => _processor.Process(heic[..200]));
        Assert.Contains("HEIC", ex.Message);
    }

    [Fact]
    public void Rejects_empty_and_oversized_files()
    {
        Assert.Throws<ImageValidationException>(() => _processor.Process([]));
        Assert.Throws<ImageValidationException>(() => _processor.Process(new byte[PhotoUploadLimits.MaxBytes + 1]));
    }

    [Theory]
    [InlineData(1 / 250.0, "1/250")]
    [InlineData(1 / 8000.0, "1/8000")]
    [InlineData(0.5, "0.5s")]
    [InlineData(2.0, "2s")]
    [InlineData(30.0, "30s")]
    public void Formats_exposure_time_like_a_camera(double seconds, string expected)
    {
        Assert.Equal(expected, ImageProcessor.FormatExposure(seconds));
    }

    private static void AssertJpegSize(byte[] jpeg, int width, int height)
    {
        using var codec = SKCodec.Create(new MemoryStream(jpeg));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal((width, height), (codec.Info.Width, codec.Info.Height));
    }

    private static bool IsRed(SKColor c) => c.Red > 200 && c.Green < 80 && c.Blue < 80;
}
