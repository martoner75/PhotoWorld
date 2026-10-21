using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Tests;

public class PhotoTagsTests
{
    [Theory]
    [InlineData("#Fog", "fog")]
    [InlineData("  Street_Photography ", "street_photography")]
    [InlineData("Città!", "città")]
    [InlineData("#", null)]
    [InlineData("--", null)]
    public void Tags_are_normalised(string raw, string? expected)
    {
        Assert.Equal(expected, PhotoTags.Normalize(raw));
    }

    [Fact]
    public void Tags_come_from_the_field_and_description_hashtags_once_each()
    {
        var tags = PhotoTags.Parse("fog, #Wildlife london", "Early morning #fog in Richmond #RedDeer, see www.x.com/#anchor");
        Assert.Equal(["fog", "wildlife", "london", "reddeer"], tags);
    }

    [Fact]
    public void A_photo_has_at_most_ten_tags()
    {
        Assert.Equal(PhotoTags.MaxPerPhoto, PhotoTags.Parse(string.Join(' ', Enumerable.Range(1, 20).Select(i => $"t{i}"))).Count);
    }

    private static Photo Shot(string title, string? description, params string[] tags) => new()
    {
        Title = title, Description = description, DisplayPath = "d", ThumbnailPath = "t",
        Tags = tags.Select(t => new PhotoTag { Name = t }).ToList(),
    };

    private static readonly Photo[] Photos =
    [
        Shot("Misty morning", null, "fog", "london"),
        Shot("Stag", "Rutting season in the park", "wildlife"),
        Shot("Bridge", null, "london"),
    ];

    private static string[] Find(PhotoSearch search) => Photos.AsQueryable().Matching(search).Select(p => p.Title).ToArray();

    [Fact]
    public void Text_matches_titles_descriptions_and_tags()
    {
        Assert.Equal(["Misty morning"], Find(new PhotoSearch { Text = "misty" }));
        Assert.Equal(["Stag"], Find(new PhotoSearch { Text = "rutting" }));
        Assert.Equal(["Misty morning"], Find(new PhotoSearch { Text = "fog" }));
    }

    [Fact]
    public void Feedback_filter_keeps_photos_asking_for_it()
    {
        var photos = new[] { Shot("Asking", null), Shot("Not asking", null) };
        photos[0].WantsFeedback = true;
        Assert.Equal(["Asking"], photos.AsQueryable().Matching(new PhotoSearch { WantsFeedback = true }).Select(p => p.Title));
        Assert.Equal(2, photos.AsQueryable().Matching(new PhotoSearch { WantsFeedback = false }).Count());
    }

    [Fact]
    public void Tag_filter_matches_exact_tags_with_or_without_hash()
    {
        Assert.Equal(["Misty morning", "Bridge"], Find(new PhotoSearch { Tag = "#London" }));
        Assert.Empty(Find(new PhotoSearch { Tag = "lond" }));
        Assert.Equal([("tag", "london")], new PhotoSearch { Tag = "#London" }.QueryValues());
    }
}
