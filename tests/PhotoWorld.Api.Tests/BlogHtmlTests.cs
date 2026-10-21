using PhotoWorld.Api.Services.Blogs;
using PhotoWorld.Shared.Blogs;

namespace PhotoWorld.Api.Tests;

public class BlogHtmlTests
{
    private static readonly Guid Mine = Guid.NewGuid();
    private static readonly Guid NotMine = Guid.NewGuid();
    private const string MineUrl = "https://example.supabase.co/storage/v1/object/public/photos/me/x/display.jpg";
    private static readonly Dictionary<Guid, string> Own = new() { [Mine] = MineUrl };

    private readonly BlogHtml _html = new();

    private CleanedPost Clean(string html) => _html.Clean(html, Own);

    [Fact]
    public void Keeps_basic_formatting()
    {
        var result = Clean("<h2>Trip</h2><p>A <strong>bold</strong>, <em>quiet</em> <u>morning</u> <s>evening</s>.</p>" +
                           "<ul><li>One</li></ul><ol><li>Two</li></ol><blockquote>Quote</blockquote>");

        foreach (var tag in new[] { "<h2>", "<strong>", "<em>", "<u>", "<s>", "<ul>", "<ol>", "<li>", "<blockquote>" })
            Assert.Contains(tag, result.Html);
    }

    [Theory]
    [InlineData("<p>Hi<script>alert(1)</script></p>", "script")]
    [InlineData("<p onclick=\"alert(1)\">Hi</p>", "onclick")]
    [InlineData("<p style=\"position:fixed\">Hi</p>", "style")]
    [InlineData("<p><span style=\"background-image:url(https://evil.example/x.png)\">Hi</span></p>", "evil.example")]
    [InlineData("<p><span style=\"color:red;position:fixed;top:0\">Hi</span></p>", "position")]
    [InlineData("<p><a href=\"javascript:alert(1)\">x</a></p>", "javascript:")]
    [InlineData("<p><iframe src=\"https://evil.example\"></iframe>Hi</p>", "iframe")]
    [InlineData("<p class=\"evil\">Hi</p>", "evil")]
    public void Strips_dangerous_markup(string input, string forbidden)
    {
        Assert.DoesNotContain(forbidden, Clean(input).Html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<p>A <span style=\"color: rgb(255, 138, 128);\">red</span> word</p>")]
    [InlineData("<p>A <span style=\"color: #ff8a80\">red</span> word</p>")]
    [InlineData("<p>A <strong><span style=\"color: #ff8a80\">red</span></strong> word</p>")]
    public void Keeps_text_colour(string input)
    {
        var result = Clean(input);

        Assert.Matches("<span style=\"color: [^\"]+\">red</span>", result.Html);
        Assert.Equal("A red word", result.Excerpt);
    }

    [Fact]
    public void Links_open_safely_in_a_new_tab()
    {
        var result = Clean("<p><a href=\"https://example.com\">site</a></p>");

        Assert.Contains("href=\"https://example.com\"", result.Html);
        Assert.Contains("rel=\"nofollow noopener noreferrer\"", result.Html);
        Assert.Contains("target=\"_blank\"", result.Html);
    }

    [Fact]
    public void Keeps_own_photo_and_rewrites_its_source()
    {
        var result = Clean($"<figure class=\"pw-photo\" data-photo-id=\"{Mine}\"><img src=\"https://evil.example/x.jpg\" onerror=\"alert(1)\" alt=\"Dawn\"><figcaption>At dawn</figcaption></figure>");

        Assert.Equal([Mine], result.PhotoIds);
        Assert.Contains($"src=\"{MineUrl}\"", result.Html);
        Assert.Contains($"data-photo-id=\"{Mine}\"", result.Html);
        Assert.Contains("<figcaption>At dawn</figcaption>", result.Html);
        Assert.DoesNotContain("evil.example", result.Html);
        Assert.DoesNotContain("onerror", result.Html);
    }

    [Fact]
    public void Drops_other_peoples_photos_and_foreign_images()
    {
        var result = Clean($"<p>Text</p><figure class=\"pw-photo\" data-photo-id=\"{NotMine}\"><img src=\"{MineUrl}\"></figure>" +
                           "<p><img src=\"https://evil.example/track.gif\"></p>");

        Assert.Empty(result.PhotoIds);
        Assert.DoesNotContain("<img", result.Html);
        Assert.DoesNotContain("<figure", result.Html);
    }

    [Fact]
    public void Wraps_a_bare_own_image_in_a_photo_figure()
    {
        var result = Clean($"<p><img data-photo-id=\"{Mine}\" src=\"x\"></p>");

        Assert.Equal([Mine], result.PhotoIds);
        Assert.Contains($"<figure class=\"pw-photo\" data-photo-id=\"{Mine}\"><img src=\"{MineUrl}\"", result.Html);
    }

    [Fact]
    public void Finds_referenced_photo_ids_in_raw_html()
    {
        var ids = _html.ReferencedPhotoIds($"<figure data-photo-id=\"{Mine}\"></figure><img data-photo-id=\"{NotMine}\"><img data-photo-id=\"nope\">");
        Assert.Equal(new HashSet<Guid> { Mine, NotMine }, ids);
    }

    [Fact]
    public void Removes_photos_the_reader_can_no_longer_see()
    {
        var stored = Clean($"<p>Before</p><figure class=\"pw-photo\" data-photo-id=\"{Mine}\"><img src=\"x\"></figure><p>After</p>").Html;

        Assert.Contains("<figure", _html.WithoutPhotosExcept(stored, new HashSet<Guid> { Mine }));
        var without = _html.WithoutPhotosExcept(stored, new HashSet<Guid>());
        Assert.DoesNotContain("<figure", without);
        Assert.Contains("<p>Before</p>", without);
        Assert.Contains("<p>After</p>", without);
    }

    [Fact]
    public void Empty_post_is_flagged()
    {
        Assert.True(Clean("<p><br></p><p>   </p>").IsEmpty);
        Assert.False(Clean($"<figure class=\"pw-photo\" data-photo-id=\"{Mine}\"><img src=\"x\"></figure>").IsEmpty);
    }

    [Fact]
    public void Excerpt_is_plain_text_cut_at_a_word()
    {
        var longText = string.Join(" ", Enumerable.Repeat("light", 100));
        var result = Clean($"<h2>Title</h2><p><strong>{longText}</strong></p>");

        Assert.StartsWith("Title light light", result.Excerpt);
        Assert.EndsWith("…", result.Excerpt);
        Assert.True(result.Excerpt.Length <= BlogLimits.ExcerptLength + 1);
        Assert.DoesNotContain("<", result.Excerpt);
    }
}
