using PhotoWorld.Shared.Walks;

namespace PhotoWorld.Api.Services;

/// <summary>The pure rules behind photo walks, kept apart so they're easy to test.</summary>
public static class WalkRules
{
    /// <summary>Problems with a walk form, as (field, message) pairs; none means it can be saved.</summary>
    public static IEnumerable<(string Field, string Message)> CannotSave(string? title, string? meetingPoint, DateTimeOffset startsAt,
        DateTimeOffset endsAt, DateTimeOffset now, bool isNew, int attendees, int? maxPeople)
    {
        if (string.IsNullOrWhiteSpace(title)) yield return ("title", "Give the walk a title.");
        if (string.IsNullOrWhiteSpace(meetingPoint)) yield return ("meetingPoint", "Say where to meet.");
        if (isNew && startsAt <= now) yield return ("startsAt", "Pick a time in the future.");
        if (endsAt <= startsAt) yield return ("endsAt", "The end must be after the start.");
        else if (endsAt - startsAt > WalkLimits.MaxLength) yield return ("endsAt", "A walk can last 12 hours at most.");
        if (maxPeople is { } max && max < attendees + 1)
            yield return ("maxPeople", $"{attendees} {(attendees == 1 ? "member is" : "members are")} already going: allow at least {attendees + 1}.");
    }

    /// <summary>Why a member can't join, or null if they can. Counts include the organiser.</summary>
    public static string? CannotJoin(bool isOrganizer, bool cancelled, DateTimeOffset startsAt, DateTimeOffset now, int attendees,
        int? maxPeople, bool blocked) =>
        isOrganizer ? "You're organising this walk."
        : cancelled ? "This walk was cancelled."
        : now >= startsAt ? "This walk has already started."
        : blocked ? "You can't join this walk."
        : maxPeople is { } max && attendees + 1 >= max ? "This walk is full."
        : null;

    /// <summary>Attendees get a reminder once, during the day before the walk.</summary>
    public static bool ReminderDue(DateTimeOffset startsAt, bool cancelled, bool sent, DateTimeOffset now) =>
        !cancelled && !sent && now < startsAt && startsAt - now <= TimeSpan.FromDays(1);
}
