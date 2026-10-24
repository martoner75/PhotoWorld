using PhotoWorld.Api.Services.Blogs;
using PhotoWorld.Shared.Blogs;

namespace PhotoWorld.Api.Tests;

public class BlogReviewTests
{
    [Theory]
    [InlineData(null, BlogPostStatus.Pending)]
    [InlineData(BlogPostStatus.Draft, BlogPostStatus.Pending)]
    [InlineData(BlogPostStatus.Pending, BlogPostStatus.Pending)]
    [InlineData(BlogPostStatus.Rejected, BlogPostStatus.Pending)]
    [InlineData(BlogPostStatus.Published, BlogPostStatus.Published)]
    public void Publishing_goes_through_the_queue_unless_already_approved(string? current, string expected)
    {
        Assert.Equal(expected, BlogReview.StatusAfterSave(current, publish: true, authorIsAdmin: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(BlogPostStatus.Draft)]
    [InlineData(BlogPostStatus.Rejected)]
    public void Admins_publish_their_own_posts_straight_away(string? current)
    {
        Assert.Equal(BlogPostStatus.Published, BlogReview.StatusAfterSave(current, publish: true, authorIsAdmin: true));
    }

    [Theory]
    [InlineData(BlogPostStatus.Pending)]
    [InlineData(BlogPostStatus.Published)]
    [InlineData(BlogPostStatus.Rejected)]
    public void Saving_without_publishing_makes_a_draft(string current)
    {
        Assert.Equal(BlogPostStatus.Draft, BlogReview.StatusAfterSave(current, publish: false, authorIsAdmin: false));
        Assert.Equal(BlogPostStatus.Draft, BlogReview.StatusAfterSave(current, publish: false, authorIsAdmin: true));
    }

    [Fact]
    public void Only_pending_posts_can_be_reviewed()
    {
        Assert.True(BlogReview.CanReview(BlogPostStatus.Pending));
        Assert.False(BlogReview.CanReview(BlogPostStatus.Draft));
        Assert.False(BlogReview.CanReview(BlogPostStatus.Published));
        Assert.False(BlogReview.CanReview(BlogPostStatus.Rejected));
    }
}
