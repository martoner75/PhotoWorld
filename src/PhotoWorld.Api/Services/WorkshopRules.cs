using PhotoWorld.Shared.Workshops;

namespace PhotoWorld.Api.Services;

/// <summary>The pure rules behind online workshops, kept apart so they're easy to test.</summary>
public static class WorkshopRules
{
    /// <summary>Problems with a workshop form, as (field, message) pairs; none means it can be saved.</summary>
    public static IEnumerable<(string Field, string Message)> CannotSave(string? title, DateTimeOffset startsAt, DateTimeOffset endsAt,
        string? videoLink, DateTimeOffset now, bool isNew, int attendees, int? maxPeople)
    {
        if (string.IsNullOrWhiteSpace(title)) yield return ("title", "Give the workshop a title.");
        if (isNew && startsAt <= now) yield return ("startsAt", "Pick a time in the future.");
        if (endsAt <= startsAt) yield return ("endsAt", "The end must be after the start.");
        else if (endsAt - startsAt > WorkshopLimits.MaxLength) yield return ("endsAt", "A workshop can last 8 hours at most.");
        if (!string.IsNullOrWhiteSpace(videoLink) && !IsVideoLink(videoLink))
            yield return ("videoLink", "Use the full https:// link to your Zoom, Meet or Teams call.");
        if (maxPeople is { } max && max < attendees + 1)
            yield return ("maxPeople", $"{attendees} {(attendees == 1 ? "member is" : "members are")} already going: allow at least {attendees + 1}.");
    }

    /// <summary>Only plain https links, so a link on the page can't run script or point at a local file.</summary>
    public static bool IsVideoLink(string link) =>
        Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);

    /// <summary>Why a member can't join, or null if they can. Counts include the host.</summary>
    public static string? CannotJoin(bool isHost, bool cancelled, DateTimeOffset endsAt, DateTimeOffset now, int attendees,
        int? maxPeople, bool blocked) =>
        isHost ? "You're hosting this workshop."
        : cancelled ? "This workshop was cancelled."
        : now >= endsAt ? "This workshop has ended."
        : blocked ? "You can't join this workshop."
        : maxPeople is { } max && attendees + 1 >= max ? "This workshop is full."
        : null;

    /// <summary>
    /// Why a member can't post in the chat, or null if they can: the host and people going can, from joining until a day
    /// after it ends.
    /// </summary>
    public static string? CannotChat(bool isHost, bool isGoing, bool cancelled, DateTimeOffset endsAt, DateTimeOffset now) =>
        !isHost && !isGoing ? "Join the workshop to take part in the chat."
        : cancelled ? "This workshop was cancelled, so its chat is closed."
        : now >= endsAt + WorkshopLimits.ChatOpenAfter ? "The chat closed a day after the workshop ended."
        : null;

    /// <summary>Attendees get a reminder once, during the day before the workshop.</summary>
    public static bool ReminderDue(DateTimeOffset startsAt, bool cancelled, bool sent, DateTimeOffset now) =>
        !cancelled && !sent && now < startsAt && startsAt - now <= TimeSpan.FromDays(1);
}
