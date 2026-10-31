using PhotoWorld.Shared.Collabs;

namespace PhotoWorld.Api.Services;

/// <summary>The pure rules behind collab calls, kept apart so they're easy to test.</summary>
public static class CollabRules
{
    /// <summary>Problems with a call's form, as (field, message) pairs; none means it can be saved.</summary>
    public static IEnumerable<(string Field, string Message)> CannotSave(string? title, string? role, string? terms, string? area,
        DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now, bool isNew, int photoCount)
    {
        if (string.IsNullOrWhiteSpace(title)) yield return ("title", "Give the call a title.");
        if (role is null || !CollabRoles.All.Contains(role)) yield return ("role", "Say who you're looking for.");
        if (terms is null || !CollabTerms.All.Contains(terms)) yield return ("terms", "Say whether it's paid, a trade for photos or unpaid.");
        if (string.IsNullOrWhiteSpace(area)) yield return ("area", "Say roughly where it is, for example the city and the neighbourhood.");
        if (isNew && startsAt <= now) yield return ("startsAt", "Pick a time in the future.");
        if (endsAt <= startsAt) yield return ("endsAt", "The end must be after the start.");
        else if (endsAt - startsAt > CollabLimits.MaxLength) yield return ("endsAt", "A collab can last 12 hours at most.");
        if (photoCount > CollabLimits.MaxPhotos) yield return ("photoIds", $"Pick up to {CollabLimits.MaxPhotos} mood photos.");
    }

    /// <summary>Why a member can't post another call, or null if they can.</summary>
    public static string? CannotPost(int openCalls) =>
        openCalls >= CollabLimits.MaxOpenCalls
            ? $"You already have {CollabLimits.MaxOpenCalls} open calls. Wait for one to happen, or cancel one, before posting another."
            : null;

    /// <summary>Why a member can't reply to a call, or null if they can. Replies close when the shoot starts.</summary>
    public static string? CannotReply(bool isPoster, bool approved, bool cancelled, DateTimeOffset startsAt, DateTimeOffset now,
        bool blocked) =>
        isPoster ? "This is your call."
        : cancelled ? "This call was cancelled."
        : !approved ? "This call isn't on the board yet."
        : now >= startsAt ? "This call has closed."
        : blocked ? "You can't reply to this call."
        : null;

    /// <summary>Why the poster can't accept a reply, or null if they can.</summary>
    public static string? CannotAccept(bool cancelled, DateTimeOffset endsAt, DateTimeOffset now, int acceptedAlready) =>
        cancelled ? "This call was cancelled."
        : now >= endsAt ? "This collab is over."
        : acceptedAlready >= CollabLimits.MaxAccepted ? $"You can accept up to {CollabLimits.MaxAccepted} people on one call."
        : null;

    /// <summary>
    /// Why a member can't post in the chat, or null if they can: the poster and the people they accepted, until a week
    /// after the shoot.
    /// </summary>
    public static string? CannotChat(bool isPoster, bool isAccepted, bool cancelled, DateTimeOffset endsAt, DateTimeOffset now) =>
        !isPoster && !isAccepted ? "The chat is for the poster and the people they accepted."
        : cancelled ? "This collab was cancelled, so its chat is closed."
        : now >= endsAt + CollabLimits.ChatOpenAfter ? "The chat closed a week after the collab."
        : null;

    /// <summary>
    /// Why someone can't confirm a shoot happened, or null if they can: the poster (for someone they accepted) or that
    /// person, once it has started and for a while after it ended.
    /// </summary>
    public static string? CannotConfirm(bool isPosterOrMember, bool accepted, bool cancelled, DateTimeOffset startsAt,
        DateTimeOffset endsAt, DateTimeOffset now) =>
        !isPosterOrMember ? "Only the two people who shot together can confirm it."
        : !accepted ? "Only an accepted reply can be confirmed."
        : cancelled ? "This collab was cancelled."
        : now < startsAt ? "You can confirm it once the shoot has started."
        : now >= endsAt + CollabLimits.ConfirmWithin ? "It's too long ago to confirm now."
        : null;

    /// <summary>A reply counts as a confirmed collab, for both of them, when both sides said it happened.</summary>
    public static bool IsConfirmed(string status, DateTimeOffset? posterConfirmedAt, DateTimeOffset? memberConfirmedAt) =>
        status == CollabReplyStatus.Accepted && posterConfirmedAt is not null && memberConfirmedAt is not null;

    /// <summary>The poster and the people accepted get a reminder once, during the day before.</summary>
    public static bool ReminderDue(DateTimeOffset startsAt, bool cancelled, bool sent, DateTimeOffset now) =>
        !cancelled && !sent && now < startsAt && startsAt - now <= TimeSpan.FromDays(1);

    /// <summary>Once it's over, everyone is asked once to confirm it happened (only if anyone was accepted).</summary>
    public static bool ConfirmRequestDue(DateTimeOffset endsAt, bool cancelled, bool sent, DateTimeOffset now) =>
        !cancelled && !sent && now >= endsAt && now < endsAt + CollabLimits.ConfirmWithin;
}
