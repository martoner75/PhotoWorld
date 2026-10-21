using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Walks;

namespace PhotoWorld.Api.Tests;

public class WalkTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_sensible_walk_can_be_saved()
    {
        Assert.Empty(WalkRules.CannotSave("Fog at dawn", "Richmond Gate", Now.AddDays(3), Now.AddDays(3).AddHours(2), Now, true, 0, 10));
    }

    [Theory]
    [InlineData("", "Gate", 24, 2, null, 0, "title")]
    [InlineData("Walk", " ", 24, 2, null, 0, "meetingPoint")]
    [InlineData("Walk", "Gate", -1, 2, null, 0, "startsAt")]
    [InlineData("Walk", "Gate", 24, -1, null, 0, "endsAt")]
    [InlineData("Walk", "Gate", 24, 13, null, 0, "endsAt")]
    [InlineData("Walk", "Gate", 24, 2, 3, 5, "maxPeople")]
    public void Bad_walks_are_refused(string title, string point, int startHours, int lengthHours, int? max, int attendees, string field)
    {
        var start = Now.AddHours(startHours);
        Assert.Contains(WalkRules.CannotSave(title, point, start, start.AddHours(lengthHours), Now, true, attendees, max), p => p.Field == field);
    }

    [Fact]
    public void Joining_follows_the_rules()
    {
        var start = Now.AddDays(1);
        Assert.Null(WalkRules.CannotJoin(false, false, start, Now, attendees: 3, maxPeople: 10, blocked: false));
        Assert.Contains("organising", WalkRules.CannotJoin(true, false, start, Now, 3, 10, false));
        Assert.Contains("cancelled", WalkRules.CannotJoin(false, true, start, Now, 3, 10, false));
        Assert.Contains("started", WalkRules.CannotJoin(false, false, Now, Now, 3, 10, false));
        Assert.Contains("full", WalkRules.CannotJoin(false, false, start, Now, attendees: 9, maxPeople: 10, blocked: false));
        Assert.Contains("can't", WalkRules.CannotJoin(false, false, start, Now, 3, 10, true));
    }

    [Fact]
    public void The_reminder_is_due_the_day_before_only_once()
    {
        Assert.False(WalkRules.ReminderDue(Now.AddDays(2), false, false, Now));
        Assert.True(WalkRules.ReminderDue(Now.AddHours(20), false, false, Now));
        Assert.False(WalkRules.ReminderDue(Now.AddHours(20), false, true, Now));
        Assert.False(WalkRules.ReminderDue(Now.AddHours(20), true, false, Now));
        Assert.False(WalkRules.ReminderDue(Now.AddHours(-1), false, false, Now));
    }

    [Fact]
    public void Walk_tags_are_valid_photo_tags()
    {
        var tag = WalkLimits.TagFor(Guid.Parse("1a2b3c4d-0000-0000-0000-000000000000"));
        Assert.Equal("walk-1a2b3c4d", tag);
        Assert.Equal(tag, PhotoTags.Normalize("#" + tag));
    }

    [Fact]
    public void Walk_news_reads_well_and_has_its_own_switch()
    {
        var item = new ActivityDto(Guid.NewGuid(), ActivityTypes.WalkJoined, new UserSummaryDto(Guid.NewGuid(), "Anna", null), null, null, null,
            null, null, null, null, null, null, Now, false, Guid.NewGuid(), "Fog at dawn");
        Assert.Equal("Anna is coming on your photo walk “Fog at dawn”", ActivityText.Describe(item));
        Assert.False(ActivityRules.Wants(ActivitySettingsDto.AllOn with { Walks = false }, ActivityTypes.WalkReminder));
        Assert.True(ActivityRules.Wants(ActivitySettingsDto.AllOn, ActivityTypes.WalkNew));
    }
}
