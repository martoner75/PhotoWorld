using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Collabs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Tests;

public class CollabTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Poster = Guid.NewGuid();
    private static readonly Guid Viewer = Guid.NewGuid();

    [Fact]
    public void A_sensible_call_can_be_saved()
    {
        var start = Now.AddDays(5);
        Assert.Empty(CollabRules.CannotSave("Golden-hour portraits", CollabRoles.Model, CollabTerms.Trade, "Naples · Centro storico",
            start, start.AddHours(2), Now, isNew: true, photoCount: 3));
    }

    [Theory]
    [InlineData("", CollabRoles.Model, CollabTerms.Paid, "Naples", 24, 2, 0, "title")]
    [InlineData("Shoot", "dog walker", CollabTerms.Paid, "Naples", 24, 2, 0, "role")]
    [InlineData("Shoot", null, CollabTerms.Paid, "Naples", 24, 2, 0, "role")]
    [InlineData("Shoot", CollabRoles.Model, "free beer", "Naples", 24, 2, 0, "terms")]
    [InlineData("Shoot", CollabRoles.Model, CollabTerms.Paid, " ", 24, 2, 0, "area")]
    [InlineData("Shoot", CollabRoles.Model, CollabTerms.Paid, "Naples", -1, 2, 0, "startsAt")]
    [InlineData("Shoot", CollabRoles.Model, CollabTerms.Paid, "Naples", 24, -1, 0, "endsAt")]
    [InlineData("Shoot", CollabRoles.Model, CollabTerms.Paid, "Naples", 24, 13, 0, "endsAt")]
    [InlineData("Shoot", CollabRoles.Model, CollabTerms.Paid, "Naples", 24, 2, 7, "photoIds")]
    public void Bad_calls_are_refused(string title, string? role, string terms, string area, int startHours, int lengthHours, int photos,
        string field)
    {
        var start = Now.AddHours(startHours);
        Assert.Contains(CollabRules.CannotSave(title, role, terms, area, start, start.AddHours(lengthHours), Now, true, photos),
            p => p.Field == field);
    }

    [Fact]
    public void A_call_already_under_way_can_still_be_edited()
    {
        var start = Now.AddHours(-1);
        Assert.Empty(CollabRules.CannotSave("Shoot", CollabRoles.Assistant, CollabTerms.Paid, "Bristol", start, start.AddHours(3), Now,
            isNew: false, photoCount: 0));
    }

    [Fact]
    public void Members_can_have_a_few_open_calls_at_once()
    {
        Assert.Null(CollabRules.CannotPost(CollabLimits.MaxOpenCalls - 1));
        Assert.Contains("already have", CollabRules.CannotPost(CollabLimits.MaxOpenCalls));
    }

    [Fact]
    public void Replies_close_when_the_shoot_starts()
    {
        var start = Now.AddDays(1);
        Assert.Null(CollabRules.CannotReply(false, approved: true, cancelled: false, start, Now, blocked: false));
        Assert.Contains("your call", CollabRules.CannotReply(true, true, false, start, Now, false));
        Assert.Contains("cancelled", CollabRules.CannotReply(false, true, true, start, Now, false));
        Assert.Contains("board", CollabRules.CannotReply(false, false, false, start, Now, false));
        Assert.Contains("closed", CollabRules.CannotReply(false, true, false, Now, Now, false));
        Assert.Contains("can't", CollabRules.CannotReply(false, true, false, start, Now, true));
    }

    [Fact]
    public void The_poster_accepts_until_the_end_and_up_to_the_limit()
    {
        var end = Now.AddHours(3);
        Assert.Null(CollabRules.CannotAccept(false, end, Now, acceptedAlready: 2));
        Assert.Contains("cancelled", CollabRules.CannotAccept(true, end, Now, 0));
        Assert.Contains("over", CollabRules.CannotAccept(false, Now, Now, 0));
        Assert.Contains("up to", CollabRules.CannotAccept(false, end, Now, CollabLimits.MaxAccepted));
    }

    [Fact]
    public void The_chat_is_for_the_poster_and_people_accepted_until_a_week_after()
    {
        var end = Now.AddHours(2);
        Assert.Null(CollabRules.CannotChat(isPoster: true, isAccepted: false, cancelled: false, end, Now));
        Assert.Null(CollabRules.CannotChat(false, true, false, end, end.AddDays(6)));
        Assert.Contains("poster", CollabRules.CannotChat(false, false, false, end, Now));
        Assert.Contains("cancelled", CollabRules.CannotChat(true, false, true, end, Now));
        Assert.Contains("closed", CollabRules.CannotChat(false, true, false, end, end.AddDays(7)));
    }

    [Fact]
    public void A_shoot_is_confirmed_once_it_started_by_the_two_people_involved()
    {
        var start = Now.AddHours(-2);
        var end = Now.AddHours(-1);
        Assert.Null(CollabRules.CannotConfirm(true, accepted: true, cancelled: false, start, end, Now));
        Assert.Contains("two people", CollabRules.CannotConfirm(false, true, false, start, end, Now));
        Assert.Contains("accepted", CollabRules.CannotConfirm(true, false, false, start, end, Now));
        Assert.Contains("cancelled", CollabRules.CannotConfirm(true, true, true, start, end, Now));
        Assert.Contains("started", CollabRules.CannotConfirm(true, true, false, Now.AddHours(1), Now.AddHours(2), Now));
        Assert.Contains("too long", CollabRules.CannotConfirm(true, true, false, start, end, end + CollabLimits.ConfirmWithin));
    }

    [Fact]
    public void Only_accepted_replies_both_sides_confirmed_count()
    {
        Assert.True(CollabRules.IsConfirmed(CollabReplyStatus.Accepted, Now, Now));
        Assert.False(CollabRules.IsConfirmed(CollabReplyStatus.Accepted, Now, null));
        Assert.False(CollabRules.IsConfirmed(CollabReplyStatus.Accepted, null, Now));
        Assert.False(CollabRules.IsConfirmed(CollabReplyStatus.Declined, Now, Now));
    }

    [Fact]
    public void The_reminder_and_the_confirm_request_go_once_at_the_right_time()
    {
        Assert.True(CollabRules.ReminderDue(Now.AddHours(20), false, false, Now));
        Assert.False(CollabRules.ReminderDue(Now.AddDays(2), false, false, Now));
        Assert.False(CollabRules.ReminderDue(Now.AddHours(20), false, true, Now));
        Assert.False(CollabRules.ReminderDue(Now.AddHours(20), true, false, Now));

        Assert.True(CollabRules.ConfirmRequestDue(Now.AddHours(-1), false, false, Now));
        Assert.False(CollabRules.ConfirmRequestDue(Now.AddHours(1), false, false, Now));
        Assert.False(CollabRules.ConfirmRequestDue(Now.AddHours(-1), false, true, Now));
        Assert.False(CollabRules.ConfirmRequestDue(Now.AddHours(-1), true, false, Now));
    }

    [Fact]
    public void The_tag_is_short_and_stable()
    {
        var id = Guid.Parse("3f9a1c2e-0000-4000-8000-000000000000");
        Assert.Equal("collab-3f9a1c2e", CollabLimits.TagFor(id));
    }

    [Fact]
    public void Every_role_and_terms_has_a_label()
    {
        Assert.All(CollabRoles.All, r => Assert.NotEqual(r, CollabRoles.Label(r)));
        Assert.All(CollabRoles.All, r => Assert.NotEqual(r, CollabRoles.PluralLabel(r)));
        Assert.All(CollabTerms.All, t => Assert.NotEqual(t, CollabTerms.Label(t)));
    }

    [Theory]
    [InlineData(CollabReviewStatus.Approved, true)]
    [InlineData(CollabReviewStatus.Pending, false)]
    [InlineData(CollabReviewStatus.Rejected, false)]
    public void Others_see_approved_calls_only(string status, bool visible)
    {
        Assert.Equal(visible, VisibleTo(CallBy(status), Viewer));
        Assert.True(VisibleTo(CallBy(status), Poster)); // posters always see their own
    }

    [Fact]
    public void Reviewers_see_pending_calls_but_not_rejected_ones()
    {
        Assert.True(VisibleTo(CallBy(CollabReviewStatus.Pending), Viewer, reviewer: true));
        Assert.False(VisibleTo(CallBy(CollabReviewStatus.Rejected), Viewer, reviewer: true));
    }

    [Fact]
    public void Moderation_hides_calls()
    {
        Assert.False(VisibleTo(CallBy(CollabReviewStatus.Approved, ReportStatus.Upheld), Viewer));
        Assert.False(VisibleTo(CallBy(CollabReviewStatus.Approved, ReportStatus.Open, ReportStatus.Open, ReportStatus.Open), Viewer));
        Assert.True(VisibleTo(CallBy(CollabReviewStatus.Approved, ReportStatus.Open), Viewer));

        var suspended = CallBy(CollabReviewStatus.Approved);
        suspended.Poster.SuspendedAt = Now;
        Assert.False(VisibleTo(suspended, Viewer));

        var blocked = CallBy(CollabReviewStatus.Approved);
        blocked.Poster.BlockedBy = [new Block { BlockerId = Viewer, BlockedId = Poster }];
        Assert.False(VisibleTo(blocked, Viewer));
    }

    [Fact]
    public void Collab_activity_follows_its_own_switch()
    {
        var off = ActivitySettingsDto.AllOn with { Collabs = false };
        foreach (var type in new[] { ActivityTypes.CollabNew, ActivityTypes.CollabReply, ActivityTypes.CollabAccepted,
                     ActivityTypes.CollabChanged, ActivityTypes.CollabCancelled, ActivityTypes.CollabReminder, ActivityTypes.CollabConfirm })
        {
            Assert.True(ActivityRules.Wants(ActivitySettingsDto.AllOn, type));
            Assert.False(ActivityRules.Wants(off, type));
            Assert.True(ActivityRules.Wants(ActivitySettingsDto.AllOn with { Workshops = false }, type));
        }
        // The answer to a review always arrives.
        Assert.True(ActivityRules.Wants(off, ActivityTypes.CollabApproved));
        Assert.True(ActivityRules.Wants(off, ActivityTypes.CollabRejected));
    }

    [Fact]
    public void Collab_activity_reads_well()
    {
        var a = new ActivityDto(Guid.NewGuid(), ActivityTypes.CollabAccepted, new UserSummaryDto(Guid.NewGuid(), "Giulia", null), null, null,
            null, null, null, null, null, null, null, Now, false, CollabId: Guid.NewGuid(), CollabTitle: "Golden-hour portraits");
        Assert.Equal("You're in! Giulia accepted your reply to “Golden-hour portraits”", ActivityText.Describe(a));
    }

    [Fact]
    public void Every_activity_type_fits_the_column()
    {
        Assert.All(ActivityTypes.All, t => Assert.True(t.Length <= 24, t));
    }

    private static Collab CallBy(string reviewStatus, params string[] reportStatuses) => new()
    {
        PosterId = Poster,
        Poster = new User { Id = Poster, DisplayName = "poster" },
        Title = "t", Role = CollabRoles.Model, Terms = CollabTerms.Trade, Area = "a", MeetingPointName = "m",
        ReviewStatus = reviewStatus,
        Reports = reportStatuses.Select(s => new Report { Reason = "spam", Status = s, ReporterId = Guid.NewGuid() }).ToList(),
    };

    private static bool VisibleTo(Collab c, Guid user, bool reviewer = false) => Moderation.CollabVisibleTo(user, reviewer).Compile()(c);
}
