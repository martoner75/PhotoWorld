using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Api.Services.Clubs;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Tests;

public class ClubTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Viewer = Guid.NewGuid();

    private static SaveClubRequest Request(string name = "Bristol Street", string policy = ClubJoinPolicy.Open,
        double? lat = null, double? lon = null) => new(name, null, "Bristol", policy, lat, lon);

    [Fact]
    public void A_sensible_club_can_be_saved()
    {
        Assert.Empty(ClubRules.CannotSave(Request()));
        Assert.Empty(ClubRules.CannotSave(Request(lat: 51.45, lon: -2.59)));
    }

    [Theory]
    [InlineData(" ", ClubJoinPolicy.Open, null, null, "name")]
    [InlineData("Club", "invite-only", null, null, "joinPolicy")]
    [InlineData("Club", ClubJoinPolicy.Open, 51.45, null, "latitude")]
    [InlineData("Club", ClubJoinPolicy.Open, double.NaN, 1.0, "latitude")]
    public void Bad_clubs_are_refused(string name, string policy, double? lat, double? lon, string field)
    {
        Assert.Contains(ClubRules.CannotSave(Request(name, policy, lat, lon)), p => p.Field == field);
    }

    [Fact]
    public void Joining_follows_the_limits()
    {
        Assert.Null(ClubRules.CannotJoin(alreadyIn: false, clubsJoined: 3, clubMembers: 40, blockedByOwner: false));
        Assert.Contains("already", ClubRules.CannotJoin(true, 3, 40, false));
        Assert.Contains("can't join", ClubRules.CannotJoin(false, 3, 40, blockedByOwner: true));
        Assert.Contains("up to", ClubRules.CannotJoin(false, ClubLimits.MaxJoined, 40, false));
        Assert.Contains("full", ClubRules.CannotJoin(false, 3, ClubLimits.MaxMembers, false));
    }

    [Fact]
    public void Open_clubs_let_people_straight_in_and_others_take_requests()
    {
        Assert.Equal(ClubMemberStatus.Active, ClubRules.StatusOnJoin(ClubJoinPolicy.Open));
        Assert.Equal(ClubMemberStatus.Pending, ClubRules.StatusOnJoin(ClubJoinPolicy.Approval));
    }

    [Fact]
    public void The_owner_hands_over_before_leaving()
    {
        Assert.NotNull(ClubRules.CannotLeave(ClubRoles.Owner));
        Assert.Null(ClubRules.CannotLeave(ClubRoles.Admin));
        Assert.Null(ClubRules.CannotLeave(ClubRoles.Member));
    }

    [Theory]
    [InlineData(ClubRoles.Owner, ClubRoles.Admin, true)]
    [InlineData(ClubRoles.Owner, ClubRoles.Member, true)]
    [InlineData(ClubRoles.Admin, ClubRoles.Member, true)]
    [InlineData(ClubRoles.Admin, ClubRoles.Admin, false)]
    [InlineData(ClubRoles.Admin, ClubRoles.Owner, false)]
    [InlineData(ClubRoles.Member, ClubRoles.Member, false)]
    public void Who_can_remove_whom(string actor, string target, bool allowed)
    {
        Assert.Equal(allowed, ClubRules.CannotRemove(actor, target, isSelf: false) is null);
    }

    [Fact]
    public void Nobody_removes_themself()
    {
        Assert.NotNull(ClubRules.CannotRemove(ClubRoles.Owner, ClubRoles.Owner, isSelf: true));
    }

    [Fact]
    public void Only_the_owner_changes_roles_and_only_for_active_members()
    {
        Assert.Null(ClubRules.CannotSetRole(ClubRoles.Owner, ClubMemberStatus.Active, ClubRoles.Admin, isSelf: false));
        Assert.Null(ClubRules.CannotSetRole(ClubRoles.Owner, ClubMemberStatus.Active, ClubRoles.Owner, isSelf: false));
        Assert.NotNull(ClubRules.CannotSetRole(ClubRoles.Admin, ClubMemberStatus.Active, ClubRoles.Admin, isSelf: false));
        Assert.NotNull(ClubRules.CannotSetRole(ClubRoles.Owner, ClubMemberStatus.Pending, ClubRoles.Admin, isSelf: false));
        Assert.NotNull(ClubRules.CannotSetRole(ClubRoles.Owner, ClubMemberStatus.Active, ClubRoles.Member, isSelf: true));
        Assert.NotNull(ClubRules.CannotSetRole(ClubRoles.Owner, ClubMemberStatus.Active, "president", isSelf: false));
    }

    [Fact]
    public void Distances_are_great_circle_kilometres()
    {
        Assert.InRange(ClubRules.DistanceKm(51.5072, -0.1276, 48.8566, 2.3522), 340, 350); // London to Paris
        Assert.Equal(0, ClubRules.DistanceKm(51.45, -2.59, 51.45, -2.59), 6);
    }

    [Fact]
    public void A_club_area_is_rounded_to_about_a_kilometre()
    {
        Assert.Equal(51.45, ClubRules.Area(51.45412));
        Assert.Null(ClubRules.Area(null));
    }

    private static Club ClubWith(string review, params string[] reportStatuses) => new()
    {
        OwnerId = Owner,
        Owner = new User { DisplayName = "Owner", Email = "o@example.com" },
        Name = "Bristol Street",
        ReviewStatus = review,
        Reports = reportStatuses.Select(s => new Report { Reason = "spam", Status = s, ReporterId = Guid.NewGuid() }).ToList(),
    };

    private static bool VisibleTo(Club club, Guid user, bool reviewer = false) => Moderation.ClubVisibleTo(user, reviewer).Compile()(club);

    [Fact]
    public void Clubs_waiting_for_approval_are_seen_by_their_owner_and_admins_only()
    {
        var pending = ClubWith(ClubReviewStatus.Pending);
        Assert.True(VisibleTo(pending, Owner));
        Assert.True(VisibleTo(pending, Viewer, reviewer: true));
        Assert.False(VisibleTo(pending, Viewer));
        Assert.True(VisibleTo(ClubWith(ClubReviewStatus.Approved), Viewer));
        Assert.False(VisibleTo(ClubWith(ClubReviewStatus.Rejected), Viewer, reviewer: true));
    }

    [Fact]
    public void Reports_hide_a_club_like_any_other_content()
    {
        Assert.True(VisibleTo(ClubWith(ClubReviewStatus.Approved, ReportStatus.Open, ReportStatus.Open), Viewer));
        Assert.False(VisibleTo(ClubWith(ClubReviewStatus.Approved, ReportStatus.Open, ReportStatus.Open, ReportStatus.Open), Viewer));
        Assert.False(VisibleTo(ClubWith(ClubReviewStatus.Approved, ReportStatus.Upheld), Viewer));
        Assert.True(VisibleTo(ClubWith(ClubReviewStatus.Approved, ReportStatus.Upheld), Owner));
    }

    [Fact]
    public void A_club_by_a_suspended_or_blocked_owner_is_hidden()
    {
        var suspended = ClubWith(ClubReviewStatus.Approved);
        suspended.Owner.SuspendedAt = DateTimeOffset.UtcNow;
        Assert.False(VisibleTo(suspended, Viewer));

        var blocked = ClubWith(ClubReviewStatus.Approved);
        blocked.Owner.BlockedBy.Add(new Block { BlockerId = Viewer, BlockedId = Owner });
        Assert.False(VisibleTo(blocked, Viewer));
    }

    [Fact]
    public void Club_activity_follows_the_clubs_switch_but_review_answers_always_arrive()
    {
        var off = ActivitySettingsDto.AllOn with { Clubs = false };
        Assert.False(ActivityRules.Wants(off, ActivityTypes.ClubRequest));
        Assert.False(ActivityRules.Wants(off, ActivityTypes.ClubAccepted));
        Assert.True(ActivityRules.Wants(off, ActivityTypes.ClubApproved));
        Assert.True(ActivityRules.Wants(off, ActivityTypes.ClubRejected));
        Assert.True(ActivityRules.IsOncePerActor(ActivityTypes.ClubRequest));
    }

    [Fact]
    public void Members_share_their_own_photos_to_approved_clubs()
    {
        Assert.Null(ClubRules.CannotShare(isActiveMember: true, clubApproved: true, ownsPhoto: true, PhotoReviewStatus.Approved));
        // Waiting for approval is fine: it shows (and members hear) once approved.
        Assert.Null(ClubRules.CannotShare(true, true, true, PhotoReviewStatus.Pending));
        Assert.Contains("own photos", ClubRules.CannotShare(true, true, ownsPhoto: false, PhotoReviewStatus.Approved));
        Assert.Contains("Join", ClubRules.CannotShare(isActiveMember: false, true, true, PhotoReviewStatus.Approved));
        Assert.Contains("approved", ClubRules.CannotShare(true, clubApproved: false, true, PhotoReviewStatus.Approved));
        Assert.Contains("wasn't approved", ClubRules.CannotShare(true, true, true, PhotoReviewStatus.Rejected));
    }

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, ClubRoles.Owner, true)]
    [InlineData(false, ClubRoles.Admin, true)]
    [InlineData(false, ClubRoles.Member, false)]
    [InlineData(false, null, false)]
    public void A_shared_photo_comes_out_at_its_owners_wish_or_a_club_admins(bool ownsPhoto, string? role, bool allowed)
    {
        Assert.Equal(allowed, ClubRules.CanUnshare(ownsPhoto, role));
    }

    [Fact]
    public void Club_photo_alerts_have_their_own_switch_and_are_grouped_by_the_day()
    {
        Assert.True(ActivityRules.Wants(ActivitySettingsDto.AllOn, ActivityTypes.ClubPhoto));
        Assert.False(ActivityRules.Wants(ActivitySettingsDto.AllOn with { ClubPhotos = false }, ActivityTypes.ClubPhoto));
        Assert.True(ActivityRules.Wants(ActivitySettingsDto.AllOn with { Clubs = false }, ActivityTypes.ClubPhoto));
        Assert.Equal(TimeSpan.FromDays(1), ActivityRules.ClubPhotoQuietFor);
        Assert.Contains(ActivityTypes.ClubPhoto, ActivityTypes.All);
    }

    [Fact]
    public void Any_active_member_organises_walks_for_an_approved_club()
    {
        Assert.Null(ClubRules.CannotOrganizeWalk(isActiveMember: true, clubApproved: true));
        Assert.Contains("members", ClubRules.CannotOrganizeWalk(isActiveMember: false, clubApproved: true));
        Assert.Contains("approved", ClubRules.CannotOrganizeWalk(isActiveMember: true, clubApproved: false));
    }

    [Theory]
    [InlineData(ClubRoles.Owner, true, true)]
    [InlineData(ClubRoles.Admin, true, true)]
    [InlineData(ClubRoles.Member, true, false)]
    [InlineData(null, true, false)]
    [InlineData(ClubRoles.Owner, false, false)]
    public void The_clubs_owner_and_admins_run_its_challenges(string? role, bool approved, bool allowed)
    {
        Assert.Equal(allowed, ClubRules.CannotRunChallenge(role, approved) is null);
    }

    [Fact]
    public void Only_active_members_enter_a_club_challenge()
    {
        Assert.Null(ClubRules.CannotEnterClubChallenge(isActiveMember: true));
        Assert.Contains("Join the club", ClubRules.CannotEnterClubChallenge(isActiveMember: false));
    }

    [Theory]
    [InlineData(ClubRoles.Owner, true, true)]
    [InlineData(ClubRoles.Admin, true, true)]
    [InlineData(ClubRoles.Member, true, false)]
    [InlineData(null, true, false)]
    [InlineData(ClubRoles.Owner, false, false)]
    public void The_owner_and_admins_pick_the_cover_from_the_clubs_photos(string? role, bool photoInFeed, bool allowed)
    {
        Assert.Equal(allowed, ClubRules.CannotSetCover(role, photoInFeed) is null);
    }

    [Fact]
    public void Club_activity_names_the_club()
    {
        var request = new ActivityDto(Guid.NewGuid(), ActivityTypes.ClubRequest, new UserSummaryDto(Viewer, "Anna", null), null, null, null,
            null, null, null, null, null, null, DateTimeOffset.UtcNow, false, ClubId: Guid.NewGuid(), ClubName: "Bristol Street");
        Assert.Equal("Anna asked to join “Bristol Street”", ActivityText.Describe(request));
        Assert.All(new[] { ActivityTypes.ClubRequest, ActivityTypes.ClubAccepted, ActivityTypes.ClubApproved, ActivityTypes.ClubRejected },
            t => Assert.Contains(t, ActivityTypes.All));
    }
}
