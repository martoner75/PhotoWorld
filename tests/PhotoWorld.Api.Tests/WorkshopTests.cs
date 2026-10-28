using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Tests;

public class WorkshopTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_sensible_workshop_can_be_saved()
    {
        var start = Now.AddDays(3);
        Assert.Empty(WorkshopRules.CannotSave("Editing night skies", start, start.AddHours(2), "https://meet.google.com/abc-defg-hij", Now, true, 0, 20));
        Assert.Empty(WorkshopRules.CannotSave("Editing night skies", start, start.AddHours(2), null, Now, true, 0, null));
    }

    [Theory]
    [InlineData("", 24, 2, null, null, 0, "title")]
    [InlineData("Talk", -1, 2, null, null, 0, "startsAt")]
    [InlineData("Talk", 24, -1, null, null, 0, "endsAt")]
    [InlineData("Talk", 24, 9, null, null, 0, "endsAt")]
    [InlineData("Talk", 24, 2, "http://zoom.us/j/1", null, 0, "videoLink")]
    [InlineData("Talk", 24, 2, "javascript:alert(1)", null, 0, "videoLink")]
    [InlineData("Talk", 24, 2, "zoom.us/j/1", null, 0, "videoLink")]
    [InlineData("Talk", 24, 2, null, 3, 5, "maxPeople")]
    public void Bad_workshops_are_refused(string title, int startHours, int lengthHours, string? link, int? max, int attendees, string field)
    {
        var start = Now.AddHours(startHours);
        Assert.Contains(WorkshopRules.CannotSave(title, start, start.AddHours(lengthHours), link, Now, true, attendees, max), p => p.Field == field);
    }

    [Fact]
    public void A_workshop_already_under_way_can_still_be_edited()
    {
        var start = Now.AddHours(-1);
        Assert.Empty(WorkshopRules.CannotSave("Talk", start, start.AddHours(2), null, Now, isNew: false, attendees: 4, maxPeople: null));
    }

    [Fact]
    public void Joining_follows_the_rules()
    {
        var end = Now.AddDays(1);
        Assert.Null(WorkshopRules.CannotJoin(false, false, end, Now, attendees: 3, maxPeople: 10, blocked: false));
        // Unlike a walk, people can still join once it has started: it's online.
        Assert.Null(WorkshopRules.CannotJoin(false, false, Now.AddMinutes(30), Now, 3, 10, false));
        Assert.Contains("hosting", WorkshopRules.CannotJoin(true, false, end, Now, 3, 10, false));
        Assert.Contains("cancelled", WorkshopRules.CannotJoin(false, true, end, Now, 3, 10, false));
        Assert.Contains("ended", WorkshopRules.CannotJoin(false, false, Now, Now, 3, 10, false));
        Assert.Contains("full", WorkshopRules.CannotJoin(false, false, end, Now, attendees: 9, maxPeople: 10, blocked: false));
        Assert.Contains("can't", WorkshopRules.CannotJoin(false, false, end, Now, 3, 10, true));
    }

    [Fact]
    public void The_chat_is_for_people_going_until_a_day_after_the_end()
    {
        var end = Now.AddHours(2);
        Assert.Null(WorkshopRules.CannotChat(isHost: true, isGoing: false, cancelled: false, end, Now));
        Assert.Null(WorkshopRules.CannotChat(false, true, false, end, Now));
        Assert.Null(WorkshopRules.CannotChat(false, true, false, end, end.AddHours(23)));
        Assert.Contains("Join", WorkshopRules.CannotChat(false, false, false, end, Now));
        Assert.Contains("cancelled", WorkshopRules.CannotChat(true, false, true, end, Now));
        Assert.Contains("closed", WorkshopRules.CannotChat(false, true, false, end, end.AddDays(1)));
    }

    [Fact]
    public void The_reminder_is_due_the_day_before_only_once()
    {
        Assert.False(WorkshopRules.ReminderDue(Now.AddDays(2), false, false, Now));
        Assert.True(WorkshopRules.ReminderDue(Now.AddHours(20), false, false, Now));
        Assert.False(WorkshopRules.ReminderDue(Now.AddHours(20), false, true, Now));
        Assert.False(WorkshopRules.ReminderDue(Now.AddHours(20), true, false, Now));
        Assert.False(WorkshopRules.ReminderDue(Now.AddHours(-1), false, false, Now));
    }

    [Fact]
    public void Workshop_activity_follows_its_own_switch()
    {
        var off = ActivitySettingsDto.AllOn with { Workshops = false };
        foreach (var type in new[] { ActivityTypes.WorkshopNew, ActivityTypes.WorkshopJoined, ActivityTypes.WorkshopChanged,
                     ActivityTypes.WorkshopCancelled, ActivityTypes.WorkshopReminder })
        {
            Assert.True(ActivityRules.Wants(ActivitySettingsDto.AllOn, type));
            Assert.False(ActivityRules.Wants(off, type));
            Assert.True(ActivityRules.Wants(ActivitySettingsDto.AllOn with { Walks = false }, type)); // not the walks switch
        }
    }

    [Fact]
    public void Workshop_activity_reads_well()
    {
        var a = new ActivityDto(Guid.NewGuid(), ActivityTypes.WorkshopNew, new UserSummaryDto(Guid.NewGuid(), "Anna", null), null, null, null,
            null, null, null, null, null, null, Now, false, WorkshopId: Guid.NewGuid(), WorkshopTitle: "Night skies");
        Assert.Equal("Anna is hosting an online workshop: “Night skies”", ActivityText.Describe(a));
    }
}
