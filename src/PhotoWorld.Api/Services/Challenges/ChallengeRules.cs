using PhotoWorld.Shared.Challenges;

namespace PhotoWorld.Api.Services.Challenges;

/// <summary>One entry's standing: what ranking needs to know about it.</summary>
public sealed record EntryScore(Guid PhotoId, Guid OwnerId, int Votes, DateTimeOffset EnteredAt);

/// <summary>The pure rules behind photo challenges, kept apart so they're easy to test.</summary>
public static class ChallengeRules
{
    public static ChallengePhase Phase(DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now) =>
        now < startsAt ? ChallengePhase.Upcoming
        : now < endsAt ? ChallengePhase.Open
        : ChallengePhase.Closed;

    /// <summary>Most votes first; on a tie, the photo entered first ranks higher (then the id, so the order never wobbles).</summary>
    public static IReadOnlyList<EntryScore> Rank(IEnumerable<EntryScore> entries) =>
        entries.OrderByDescending(e => e.Votes).ThenBy(e => e.EnteredAt).ThenBy(e => e.PhotoId).ToList();

    /// <summary>
    /// Problems with an admin's challenge form, as (field, message) pairs; none means it can be saved. A new challenge
    /// must end in the future; an existing one may be corrected after the fact.
    /// </summary>
    public static IEnumerable<(string Field, string Message)> CannotSave(string? title, DateTimeOffset startsAt,
        DateTimeOffset endsAt, DateTimeOffset now, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(title)) yield return ("title", "Give the challenge a theme.");
        if (endsAt <= startsAt) yield return ("endsAt", "The end must be after the start.");
        else if (isNew && endsAt <= now) yield return ("endsAt", "A new challenge has to end in the future.");
        if (endsAt - startsAt > TimeSpan.FromDays(366)) yield return ("endsAt", "A challenge can run for a year at most.");
    }

    /// <summary>
    /// Why a member can't enter a photo, or null if they can. <paramref name="ownsVisiblePhoto"/> means the photo
    /// is theirs and not hidden by moderation.
    /// </summary>
    public static string? CannotEnter(ChallengePhase phase, bool ownsVisiblePhoto, bool alreadyEntered, int myEntryCount) =>
        phase == ChallengePhase.Upcoming ? "This challenge hasn't started yet."
        : phase == ChallengePhase.Closed ? "This challenge has ended."
        : !ownsVisiblePhoto ? "You can only enter your own photos."
        : alreadyEntered ? "That photo is already entered."
        : myEntryCount >= ChallengeLimits.EntriesPerMember
            ? $"You can enter up to {ChallengeLimits.EntriesPerMember} photos in a challenge."
            : null;
}
