using PhotoWorld.Api.Data;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Tests;

/// <summary>The visibility rule, evaluated in memory (EF runs the same expressions as SQL).</summary>
public class ModerationTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Viewer = Guid.NewGuid();

    private static Photo PhotoWith(params string[] reportStatuses) => new()
    {
        OwnerId = Owner,
        Title = "t",
        DisplayPath = "d",
        ThumbnailPath = "t",
        Reports = reportStatuses.Select(s => new Report { Reason = "spam", Status = s, ReporterId = Guid.NewGuid() }).ToList(),
    };

    private static bool VisibleTo(Photo photo, Guid? user) => Moderation.PhotoVisibleTo(user).Compile()(photo);

    [Fact]
    public void Two_open_reports_keep_a_photo_visible()
    {
        Assert.True(VisibleTo(PhotoWith(ReportStatus.Open, ReportStatus.Open), Viewer));
    }

    [Fact]
    public void Three_open_reports_hide_a_photo_from_others_but_not_its_owner()
    {
        var photo = PhotoWith(ReportStatus.Open, ReportStatus.Open, ReportStatus.Open);

        Assert.False(VisibleTo(photo, Viewer));
        Assert.False(VisibleTo(photo, null));
        Assert.True(VisibleTo(photo, Owner));
    }

    [Fact]
    public void One_upheld_report_removes_a_photo_for_others()
    {
        var photo = PhotoWith(ReportStatus.Upheld);

        Assert.False(VisibleTo(photo, Viewer));
        Assert.True(VisibleTo(photo, Owner));
    }

    [Fact]
    public void Dismissed_reports_do_not_count()
    {
        Assert.True(VisibleTo(PhotoWith(ReportStatus.Open, ReportStatus.Open, ReportStatus.Dismissed, ReportStatus.Dismissed), Viewer));
    }

    [Fact]
    public void Comment_rule_matches_photo_rule()
    {
        var comment = new PhotoComment
        {
            AuthorId = Owner,
            Text = "x",
            Reports = Enumerable.Range(0, 3).Select(_ => new Report { Reason = "spam", Status = ReportStatus.Open }).ToList(),
        };
        var visible = Moderation.CommentVisibleTo(Viewer).Compile();
        var visibleToAuthor = Moderation.CommentVisibleTo(Owner).Compile();

        Assert.False(visible(comment));
        Assert.True(visibleToAuthor(comment));
    }

    private static BlogPost PostWith(string status, params string[] reportStatuses) => new()
    {
        AuthorId = Owner,
        Title = "t",
        BodyHtml = "<p>x</p>",
        Excerpt = "x",
        Status = status,
        Reports = reportStatuses.Select(s => new Report { Reason = "spam", Status = s }).ToList(),
    };

    [Fact]
    public void Drafts_are_visible_only_to_their_author()
    {
        var visible = Moderation.PostVisibleTo(Viewer).Compile();
        var visibleToAuthor = Moderation.PostVisibleTo(Owner).Compile();

        Assert.False(visible(PostWith(BlogPostStatus.Draft)));
        Assert.True(visibleToAuthor(PostWith(BlogPostStatus.Draft)));
        Assert.True(visible(PostWith(BlogPostStatus.Published)));
    }

    [Fact]
    public void Posts_and_blog_comments_follow_the_same_hide_rule()
    {
        var post = PostWith(BlogPostStatus.Published, ReportStatus.Open, ReportStatus.Open, ReportStatus.Open);
        Assert.False(Moderation.PostVisibleTo(Viewer).Compile()(post));
        Assert.True(Moderation.PostVisibleTo(Owner).Compile()(post));
        Assert.False(Moderation.PostVisibleTo(Viewer).Compile()(PostWith(BlogPostStatus.Published, ReportStatus.Upheld)));

        var comment = new BlogComment { AuthorId = Owner, Text = "x", Reports = [new Report { Reason = "spam", Status = ReportStatus.Upheld }] };
        Assert.False(Moderation.BlogCommentVisibleTo(Viewer).Compile()(comment));
        Assert.True(Moderation.BlogCommentVisibleTo(Owner).Compile()(comment));
    }

    [Theory]
    [InlineData(false, 0, ModerationState.Visible)]
    [InlineData(false, 2, ModerationState.Visible)]
    [InlineData(false, 3, ModerationState.PendingReview)]
    [InlineData(true, 0, ModerationState.Removed)]
    [InlineData(true, 5, ModerationState.Removed)]
    public void State_shown_to_owner(bool upheld, int open, ModerationState expected)
    {
        Assert.Equal(expected, Moderation.State(upheld, open));
    }
}
