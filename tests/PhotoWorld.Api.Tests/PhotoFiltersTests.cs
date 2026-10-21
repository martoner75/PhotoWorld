using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Tests;

/// <summary>The camera-settings filters, run in memory (EF translates the same expressions to SQL).</summary>
public class PhotoFiltersTests
{
    private static Photo Shot(string title, string? make = null, string? model = null, string? lens = null,
        decimal? focal = null, decimal? aperture = null, int? iso = null) => new()
    {
        Title = title, DisplayPath = "d", ThumbnailPath = "t",
        CameraMake = make, CameraModel = model, Lens = lens, FocalLengthMm = focal, Aperture = aperture, Iso = iso,
    };

    private static readonly Photo[] Photos =
    [
        Shot("street", "FUJIFILM", "X-T5", "XF23mmF1.4 R LM WR", 23, 1.4m, 400),
        Shot("portrait", "SONY", "ILCE-7M4", "FE 85mm F1.8", 85, 1.8m, 100),
        Shot("landscape", "SONY", "ILCE-7M4", "FE 16-35mm F4 ZA OSS", 16, 11m, 100),
        Shot("phone"),
    ];

    private static string[] Find(PhotoSearch search) =>
        Photos.AsQueryable().Matching(search).Select(p => p.Title).ToArray();

    [Fact]
    public void No_filters_keep_every_photo()
    {
        Assert.Equal(4, Find(new PhotoSearch()).Length);
    }

    [Fact]
    public void Camera_matches_make_or_model_ignoring_case()
    {
        Assert.Equal(["portrait", "landscape"], Find(new PhotoSearch { Camera = "sony" }));
        Assert.Equal(["street"], Find(new PhotoSearch { Camera = " x-t5 " }));
    }

    [Fact]
    public void Lens_matches_part_of_the_name()
    {
        Assert.Equal(["portrait"], Find(new PhotoSearch { Lens = "85mm" }));
    }

    [Fact]
    public void Ranges_include_their_ends_and_skip_photos_without_the_setting()
    {
        Assert.Equal(["street", "portrait"], Find(new PhotoSearch { ApertureMax = 1.8m }));
        Assert.Equal(["landscape"], Find(new PhotoSearch { ApertureMin = 8 }));
        Assert.Equal(["street", "landscape"], Find(new PhotoSearch { FocalMax = 35 }));
        Assert.Equal(["portrait"], Find(new PhotoSearch { FocalMin = 85, FocalMax = 85 }));
        Assert.Equal(["street"], Find(new PhotoSearch { IsoMin = 200 }));
    }

    [Fact]
    public void Filters_combine()
    {
        Assert.Equal(["landscape"], Find(new PhotoSearch { Camera = "sony", FocalMax = 35, IsoMax = 100 }));
    }

    [Fact]
    public void Query_values_only_include_filters_that_are_set()
    {
        var search = new PhotoSearch { Camera = " X-T5 ", ApertureMax = 2.8m, IsoMin = 100, Lens = " " };

        Assert.Equal([("camera", "X-T5"), ("apertureMax", "2.8"), ("isoMin", "100")], search.QueryValues());
        Assert.False(search.IsEmpty);
        Assert.True(new PhotoSearch { Lens = "" }.IsEmpty);
    }
}
