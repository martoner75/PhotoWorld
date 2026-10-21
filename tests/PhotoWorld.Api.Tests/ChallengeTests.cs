using PhotoWorld.Api.Services.Challenges;
using PhotoWorld.Shared.Challenges;

namespace PhotoWorld.Api.Tests;

public class ChallengeTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddDays(30);

    [Fact]
    public void Phase_follows_the_start_and_end_times()
    {
        Assert.Equal(ChallengePhase.Upcoming, ChallengeRules.Phase(Start, End, Start.AddTicks(-1)));
        Assert.Equal(ChallengePhase.Open, ChallengeRules.Phase(Start, End, Start));
        Assert.Equal(ChallengePhase.Open, ChallengeRules.Phase(Start, End, End.AddTicks(-1)));
        Assert.Equal(ChallengePhase.Closed, ChallengeRules.Phase(Start, End, End));
    }

    [Fact]
    public void Most_votes_rank_first_and_ties_go_to_the_earlier_entry()
    {
        var early = new EntryScore(Guid.NewGuid(), Guid.NewGuid(), 5, Start.AddDays(1));
        var late = new EntryScore(Guid.NewGuid(), Guid.NewGuid(), 5, Start.AddDays(2));
        var leader = new EntryScore(Guid.NewGuid(), Guid.NewGuid(), 9, Start.AddDays(3));
        var last = new EntryScore(Guid.NewGuid(), Guid.NewGuid(), 0, Start);

        Assert.Equal([leader, early, late, last], ChallengeRules.Rank([last, late, leader, early]));
    }

    [Fact]
    public void Members_can_enter_their_own_photos_while_open()
    {
        Assert.Null(ChallengeRules.CannotEnter(ChallengePhase.Open, ownsVisiblePhoto: true, alreadyEntered: false, myEntryCount: 0));
    }

    [Theory]
    [InlineData(ChallengePhase.Upcoming, true, false, 0, "hasn't started")]
    [InlineData(ChallengePhase.Closed, true, false, 0, "has ended")]
    [InlineData(ChallengePhase.Open, false, false, 0, "your own photos")]
    [InlineData(ChallengePhase.Open, true, true, 1, "already entered")]
    [InlineData(ChallengePhase.Open, true, false, ChallengeLimits.EntriesPerMember, "up to")]
    public void Entries_are_refused_with_a_reason(ChallengePhase phase, bool owns, bool entered, int count, string reason)
    {
        Assert.Contains(reason, ChallengeRules.CannotEnter(phase, owns, entered, count));
    }
}
