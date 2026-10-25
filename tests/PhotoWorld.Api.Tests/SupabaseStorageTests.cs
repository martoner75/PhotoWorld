using Microsoft.Extensions.Options;
using PhotoWorld.Api.Services.Photos;

namespace PhotoWorld.Api.Tests;

public class SupabaseStorageTests
{
    private static SupabaseStorage Storage(bool imageTransforms) => new(new HttpClient(), Options.Create(
        new SupabaseStorageOptions { Url = "https://example.supabase.co/", ImageTransforms = imageTransforms }));

    [Fact]
    public void Thumbnails_are_the_stored_file_by_default()
    {
        var storage = Storage(imageTransforms: false);

        Assert.True(storage.StoresThumbnails);
        Assert.Equal("https://example.supabase.co/storage/v1/object/public/photos/u/p/thumb.jpg",
            storage.ThumbnailUrl("u/p/thumb.jpg"));
    }

    [Fact]
    public void Thumbnails_are_resized_by_Supabase_with_image_transforms_on()
    {
        var storage = Storage(imageTransforms: true);

        Assert.False(storage.StoresThumbnails);
        Assert.Equal("https://example.supabase.co/storage/v1/render/image/public/photos/u/p/display.jpg" +
                     "?width=640&height=640&resize=contain&quality=80", storage.ThumbnailUrl("u/p/display.jpg"));
    }

    [Fact]
    public void Display_images_are_never_transformed()
    {
        Assert.Equal("https://example.supabase.co/storage/v1/object/public/photos/u/p/display.jpg",
            Storage(imageTransforms: true).PublicUrl("u/p/display.jpg"));
    }
}
