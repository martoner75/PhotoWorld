using PhotoWorld.Api.Services.Blogs;
using PhotoWorld.Shared.Blogs;

namespace PhotoWorld.Api.Tests;

public class BlogAttachmentsTests
{
    [Fact]
    public void Recognises_pdfs_by_their_bytes()
    {
        Assert.True(BlogAttachments.LooksLikePdf("%PDF-1.7\n..."u8));
        Assert.False(BlogAttachments.LooksLikePdf("<html><script>"u8));
        Assert.False(BlogAttachments.LooksLikePdf("%PD"u8));
        Assert.False(BlogAttachments.LooksLikePdf([]));
    }

    [Theory]
    [InlineData("Dolomites guide.pdf", "Dolomites guide.pdf")]
    [InlineData("checklist.PDF", "checklist.pdf")]
    [InlineData("notes", "notes.pdf")]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData(@"C:\Users\me\map.pdf", "map.pdf")]
    [InlineData("a\"b<c>|d?.pdf", "a_b_c__d_.pdf")]
    [InlineData("line\r\nbreak.pdf", "line__break.pdf")]
    [InlineData("", "document.pdf")]
    [InlineData(null, "document.pdf")]
    [InlineData(".pdf", "document.pdf")]
    public void Cleans_file_names(string? input, string expected)
    {
        Assert.Equal(expected, BlogAttachments.CleanFileName(input));
    }

    [Fact]
    public void Shortens_long_file_names_and_keeps_the_extension()
    {
        var name = BlogAttachments.CleanFileName(new string('x', 500) + ".pdf");

        Assert.Equal(BlogLimits.AttachmentFileNameMax, name.Length);
        Assert.EndsWith(".pdf", name);
    }

    [Fact]
    public void Never_splits_an_emoji_when_shortening()
    {
        // The cut falls between the two halves of the first emoji, so the whole emoji goes.
        var stem = new string('x', BlogLimits.AttachmentFileNameMax - 5);

        Assert.Equal(stem + ".pdf", BlogAttachments.CleanFileName(stem + "\U0001F4F7\U0001F4F7.pdf"));
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1500, "2 KB")]
    [InlineData(3_355_443, "3.2 MB")]
    public void Formats_file_sizes(long bytes, string expected)
    {
        Assert.Equal(expected, FileSize.Format(bytes));
    }
}
