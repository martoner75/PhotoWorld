using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Challenges;
using PhotoWorld.Shared.Blogs;

namespace PhotoWorld.Api.Tests;

public class AdminTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    [Fact]
    public void Admins_can_promote_and_demote_others()
    {
        Assert.Null(AdminRules.CannotChangeAdmin(Me, Other, makeAdmin: true, targetSuspended: false));
        Assert.Null(AdminRules.CannotChangeAdmin(Me, Other, makeAdmin: false, targetSuspended: false));
    }

    [Fact]
    public void Nobody_demotes_themselves_or_promotes_a_suspended_member()
    {
        Assert.NotNull(AdminRules.CannotChangeAdmin(Me, Me, makeAdmin: false, targetSuspended: false));
        Assert.NotNull(AdminRules.CannotChangeAdmin(Me, Other, makeAdmin: true, targetSuspended: true));
    }

    [Fact]
    public void Nobody_suspends_themselves_but_reactivating_is_always_allowed()
    {
        Assert.Null(AdminRules.CannotSuspend(Me, Other, suspend: true));
        Assert.NotNull(AdminRules.CannotSuspend(Me, Me, suspend: true));
        Assert.Null(AdminRules.CannotSuspend(Me, Me, suspend: false));
    }

    [Fact]
    public void Suspended_members_content_is_hidden_from_everyone_else()
    {
        var owner = new User { Id = Guid.NewGuid(), DisplayName = "o", SuspendedAt = DateTimeOffset.UtcNow };
        var photo = new Photo { OwnerId = owner.Id, Owner = owner, Title = "t", DisplayPath = "d", ThumbnailPath = "t" };
        var post = new BlogPost { AuthorId = owner.Id, Author = owner, Title = "t", BodyHtml = "x", Excerpt = "x", Status = BlogPostStatus.Published };

        Assert.False(Moderation.PhotoVisibleTo(Other).Compile()(photo));
        Assert.False(Moderation.PhotoVisibleTo(null).Compile()(photo));
        Assert.False(Moderation.PostVisibleTo(Other).Compile()(post));
        Assert.False(Moderation.CommentVisibleTo(Other).Compile()(new PhotoComment { AuthorId = owner.Id, Author = owner, Text = "x" }));
    }

    [Fact]
    public void Suspension_message_includes_the_reason()
    {
        Assert.Equal("This PhotoCommunity account has been suspended.", SessionCheck.SuspendedMessage(null));
        Assert.Equal("This PhotoCommunity account has been suspended: repeated spam.", SessionCheck.SuspendedMessage(" repeated spam. "));
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_valid_challenge_can_be_saved()
    {
        Assert.Empty(ChallengeRules.CannotSave("Fog", Now.AddDays(10), Now.AddDays(40), Now, isNew: true));
    }

    [Theory]
    [InlineData(" ", 1, 30, true, "title")]
    [InlineData("Fog", 30, 1, true, "endsAt")]
    [InlineData("Fog", -40, -10, true, "endsAt")]
    [InlineData("Fog", 1, 400, true, "endsAt")]
    public void Invalid_challenges_are_refused(string title, int startDays, int endDays, bool isNew, string field)
    {
        Assert.Contains(ChallengeRules.CannotSave(title, Now.AddDays(startDays), Now.AddDays(endDays), Now, isNew), p => p.Field == field);
    }

    [Fact]
    public void Past_challenges_can_still_be_corrected()
    {
        Assert.Empty(ChallengeRules.CannotSave("Fog", Now.AddDays(-40), Now.AddDays(-10), Now, isNew: false));
    }
}

public class CollectionTests
{
    private static readonly Guid Viewer = Guid.NewGuid();

    private static Collection CollectionBy(User owner, bool isPublic) =>
        new() { OwnerId = owner.Id, Owner = owner, Title = "Fog", IsPublic = isPublic };

    [Fact]
    public void Private_collections_are_only_for_their_owner()
    {
        var owner = new User { Id = Guid.NewGuid(), DisplayName = "o" };
        var visible = Moderation.CollectionVisibleTo(Viewer).Compile();

        Assert.False(visible(CollectionBy(owner, isPublic: false)));
        Assert.True(visible(CollectionBy(owner, isPublic: true)));
        Assert.True(Moderation.CollectionVisibleTo(owner.Id).Compile()(CollectionBy(owner, isPublic: false)));
    }

    [Fact]
    public void Public_collections_of_blocked_or_suspended_members_are_hidden()
    {
        var blocked = new User { Id = Guid.NewGuid(), DisplayName = "b", BlockedBy = [new Block { BlockerId = Viewer }] };
        var suspended = new User { Id = Guid.NewGuid(), DisplayName = "s", SuspendedAt = DateTimeOffset.UtcNow };
        var visible = Moderation.CollectionVisibleTo(Viewer).Compile();

        Assert.False(visible(CollectionBy(blocked, isPublic: true)));
        Assert.False(visible(CollectionBy(suspended, isPublic: true)));
    }
}
