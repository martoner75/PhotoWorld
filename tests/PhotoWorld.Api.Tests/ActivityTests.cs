using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Tests;

public class ActivityTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddDays(30);

    [Theory]
    [InlineData(ActivityTypes.Like, nameof(ActivitySettingsDto.Likes))]
    [InlineData(ActivityTypes.Comment, nameof(ActivitySettingsDto.Comments))]
    [InlineData(ActivityTypes.PostComment, nameof(ActivitySettingsDto.Comments))]
    [InlineData(ActivityTypes.Feedback, nameof(ActivitySettingsDto.Comments))]
    [InlineData(ActivityTypes.Follow, nameof(ActivitySettingsDto.Follows))]
    [InlineData(ActivityTypes.NewPhoto, nameof(ActivitySettingsDto.FollowedPosts))]
    [InlineData(ActivityTypes.NewPost, nameof(ActivitySettingsDto.FollowedPosts))]
    [InlineData(ActivityTypes.ChallengeOpen, nameof(ActivitySettingsDto.Challenges))]
    [InlineData(ActivityTypes.ChallengeResult, nameof(ActivitySettingsDto.Challenges))]
    public void Each_type_follows_its_own_switch(string type, string setting)
    {
        Assert.True(ActivityRules.Wants(ActivitySettingsDto.AllOn, type));
        var off = setting switch
        {
            nameof(ActivitySettingsDto.Likes) => ActivitySettingsDto.AllOn with { Likes = false },
            nameof(ActivitySettingsDto.Comments) => ActivitySettingsDto.AllOn with { Comments = false },
            nameof(ActivitySettingsDto.Follows) => ActivitySettingsDto.AllOn with { Follows = false },
            nameof(ActivitySettingsDto.FollowedPosts) => ActivitySettingsDto.AllOn with { FollowedPosts = false },
            _ => ActivitySettingsDto.AllOn with { Challenges = false },
        };
        Assert.False(ActivityRules.Wants(off, type));
    }

    [Fact]
    public void Challenge_news_goes_out_at_the_right_moments_once()
    {
        Assert.Equal((false, false, false), ActivityRules.ChallengeNewsDue(Start, End, false, false, false, Start.AddTicks(-1)));
        Assert.Equal((true, false, false), ActivityRules.ChallengeNewsDue(Start, End, false, false, false, Start));
        Assert.Equal((false, false, false), ActivityRules.ChallengeNewsDue(Start, End, true, false, false, Start.AddDays(2)));
        Assert.Equal((false, true, false), ActivityRules.ChallengeNewsDue(Start, End, true, false, false, End.AddHours(-3)));
        Assert.Equal((false, false, true), ActivityRules.ChallengeNewsDue(Start, End, true, true, false, End));
        // Too old to be news (e.g. when the feature is switched on).
        Assert.Equal((false, false, false), ActivityRules.ChallengeNewsDue(Start, End, false, false, false, End.AddDays(5)));
    }

    [Fact]
    public void Snippets_are_tidy_and_short()
    {
        Assert.Null(ActivityRules.Snippet("  "));
        Assert.Equal("Lovely light here", ActivityRules.Snippet("Lovely   light\nhere"));
        var longText = ActivityRules.Snippet(new string('a', 500))!;
        Assert.Equal(ActivityRules.SnippetMax + 1, longText.Length);
        Assert.EndsWith("…", longText);
    }

    private static ActivityDto Item(string type, int? place = null) => new(Guid.NewGuid(), type,
        new UserSummaryDto(Guid.NewGuid(), "Anna", null), Guid.NewGuid(), "Fog", null, null, "Trip", null, "Night",
        place, null, Start, false);

    [Theory]
    [InlineData(ActivityTypes.Like, null, "Anna liked your photo “Fog”")]
    [InlineData(ActivityTypes.Follow, null, "Anna started following you")]
    [InlineData(ActivityTypes.PostComment, null, "Anna commented on your post “Trip”")]
    [InlineData(ActivityTypes.ChallengeResult, 1, "You won the “Night” challenge! 🏆")]
    [InlineData(ActivityTypes.ChallengeResult, 2, "You placed #2 in the “Night” challenge")]
    [InlineData(ActivityTypes.ChallengeResult, null, "The results of the “Night” challenge are in")]
    public void Wording_reads_naturally(string type, int? place, string expected)
    {
        Assert.Equal(expected, ActivityText.Describe(Item(type, place)));
    }

    [Fact]
    public void Photo_of_the_day_news_always_goes_out_and_reads_well()
    {
        var off = new ActivitySettingsDto(false, false, false, false, false);
        Assert.True(ActivityRules.Wants(off, ActivityTypes.PhotoOfTheDay));
        Assert.Equal("Your photo “Fog” is today's photo of the day! ⭐", ActivityText.Describe(Item(ActivityTypes.PhotoOfTheDay)));
    }

    [Fact]
    public void Review_answers_always_reach_the_author_and_read_well()
    {
        var off = new ActivitySettingsDto(false, false, false, false, false, Walks: false);
        Assert.True(ActivityRules.Wants(off, ActivityTypes.PostApproved));
        Assert.True(ActivityRules.Wants(off, ActivityTypes.PostRejected));
        Assert.Equal("Your post “Trip” was approved and is now published", ActivityText.Describe(Item(ActivityTypes.PostApproved)));
        Assert.Equal("Your post “Trip” wasn't approved. Edit it and submit it again", ActivityText.Describe(Item(ActivityTypes.PostRejected)));
        Assert.True(ActivityRules.Wants(off, ActivityTypes.PhotoApproved));
        Assert.True(ActivityRules.Wants(off, ActivityTypes.PhotoRejected));
        Assert.Equal("Your photo “Fog” was approved and is now in the gallery", ActivityText.Describe(Item(ActivityTypes.PhotoApproved)));
        Assert.Equal("Your photo “Fog” wasn't approved, so only you can see it", ActivityText.Describe(Item(ActivityTypes.PhotoRejected)));
    }

    [Fact]
    public void Times_read_as_ago()
    {
        Assert.Equal("just now", ActivityText.Ago(Start, Start.AddSeconds(20)));
        Assert.Equal("5 min ago", ActivityText.Ago(Start, Start.AddMinutes(5)));
        Assert.Equal("yesterday", ActivityText.Ago(Start, Start.AddHours(30)));
        Assert.Equal("1 Nov 2026", ActivityText.Ago(Start, Start.AddDays(10)));
    }
}
