using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Api.Services.Clubs;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Workshops;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// Online workshops: a member hosts a session on a topic, others join, and everyone going talks in its chat (and on the
/// host's video call, if they add a link). Workshops by members the viewer blocked (or who blocked the viewer), or by
/// suspended members, don't show.
/// </summary>
[ApiController]
[Authorize]
public class WorkshopsController(PhotoWorldDbContext db, AdminAccess admins, ActivityFeed activity, TimeProvider clock,
    ILogger<WorkshopsController> logger) : ControllerBase
{
    private const int MaxListed = 100;
    /// <summary>The most one poll returns; a client that fell further behind asks again.</summary>
    private const int MaxMessagesPerPoll = 200;

    [HttpGet(ApiRoutes.Workshops)]
    public async Task<ActionResult<IReadOnlyList<WorkshopSummaryDto>>> List(string? list = null, Guid? clubId = null, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var now = clock.GetUtcNow();
        var query = Visible(me);
        // One club's workshops (for its page). Visible already keeps them to its members.
        if (clubId is not null) query = query.Where(w => w.ClubId == clubId);
        query = list switch
        {
            WorkshopLists.Past => query.Where(w => w.EndsAt <= now).OrderByDescending(w => w.StartsAt),
            WorkshopLists.Mine => query.Where(w => w.HostId == me || w.Attendees.Any(a => a.UserId == me)).OrderByDescending(w => w.StartsAt),
            _ => query.Where(w => w.EndsAt > now && w.CancelledAt == null).OrderBy(w => w.StartsAt),
        };
        return await SummariesAsync(query.Take(MaxListed), me, ct);
    }

    [HttpGet(ApiRoutes.Workshop)]
    public async Task<ActionResult<WorkshopDto>> Get(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var summary = (await SummariesAsync(Visible(me).Where(w => w.Id == id), me, ct)).SingleOrDefault();
        if (summary is null) return NotFound();

        var details = await db.Workshops.AsNoTracking().Where(w => w.Id == id)
            .Select(w => new
            {
                w.Description,
                w.VideoLink,
                Attendees = w.Attendees.Where(a => a.User.SuspendedAt == null && !a.User.BlockedBy.Any(b => b.BlockerId == me))
                    .OrderBy(a => a.JoinedAt).Select(a => new UserSummaryDto(a.User.Id, a.User.DisplayName, a.User.AvatarUrl)).ToList(),
                BlockedByHost = db.Blocks.Any(b => b.BlockerId == w.HostId && b.BlockedId == me),
            })
            .SingleAsync(ct);

        var now = clock.GetUtcNow();
        var isAdmin = await admins.IsAdminAsync(me, ct);
        var inside = summary.IsHost || summary.IsGoing;
        var joinReason = summary.IsGoing ? null : WorkshopRules.CannotJoin(summary.IsHost, summary.IsCancelled, summary.EndsAt, now,
            summary.AttendeeCount, summary.MaxPeople, details.BlockedByHost);
        var chatReason = WorkshopRules.CannotChat(summary.IsHost, summary.IsGoing, summary.IsCancelled, summary.EndsAt, now);
        return new WorkshopDto(summary, details.Description, inside ? details.VideoLink : null, [summary.Host, .. details.Attendees],
            summary.IsHost || isAdmin, !summary.IsGoing && joinReason is null, inside ? null : joinReason,
            inside || isAdmin, chatReason is null, chatReason);
    }

    /// <summary>Hosts a workshop; the host's followers (or, for a club workshop, the club's members) hear about it.</summary>
    [HttpPost(ApiRoutes.Workshops)]
    public async Task<ActionResult<WorkshopSummaryDto>> Create(SaveWorkshopRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!IsValid(request, isNew: true, attendees: 0)) return ValidationProblem(ModelState);
        if (request.ClubId is { } clubId)
        {
            var club = await db.Clubs.AsNoTracking().Where(Moderation.ClubVisibleTo(me)).Where(c => c.Id == clubId)
                .Select(c => new
                {
                    Approved = c.ReviewStatus == ClubReviewStatus.Approved,
                    Member = c.Members.Any(m => m.UserId == me && m.Status == ClubMemberStatus.Active),
                })
                .SingleOrDefaultAsync(ct);
            if (club is null) return NotFound();
            if (ClubRules.CannotHostWorkshop(club.Member, club.Approved) is { } problem)
                return Problem(problem, statusCode: StatusCodes.Status400BadRequest);
        }

        var now = clock.GetUtcNow();
        var workshop = new Workshop { HostId = me, Title = "", CreatedAt = now, ClubId = request.ClubId };
        Apply(workshop, request, now);
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} is hosting workshop {WorkshopId}", me, workshop.Id);
        var recipients = workshop.ClubId is { } forClub
            ? await db.ClubMembers.Where(m => m.ClubId == forClub && m.Status == ClubMemberStatus.Active).Select(m => m.UserId).ToListAsync(ct)
            : await db.Follows.Where(f => f.FolloweeId == me).Select(f => f.FollowerId).ToListAsync(ct);
        await activity.NotifyWorkshopAsync(recipients, me, me, workshop.Id, ActivityTypes.WorkshopNew, ct);
        return CreatedAtAction(nameof(Get), new { id = workshop.Id },
            (await SummariesAsync(db.Workshops.Where(w => w.Id == workshop.Id), me, ct)).Single());
    }

    /// <summary>Changes a workshop. Attendees hear about it when the time or the video link changes.</summary>
    [HttpPut(ApiRoutes.Workshop)]
    public async Task<ActionResult<WorkshopSummaryDto>> Update(Guid id, SaveWorkshopRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var workshop = await db.Workshops.Include(w => w.Attendees).SingleOrDefaultAsync(w => w.Id == id, ct);
        if (workshop is null) return NotFound();
        if (workshop.HostId != me && !await admins.IsAdminAsync(me, ct)) return Forbid();
        if (!IsValid(request, isNew: false, attendees: workshop.Attendees.Count)) return ValidationProblem(ModelState);

        var moved = workshop.StartsAt != request.StartsAt || workshop.EndsAt != request.EndsAt
                    || workshop.VideoLink != CleanLink(request.VideoLink);
        if (workshop.StartsAt != request.StartsAt) workshop.ReminderSentAt = null; // remind again for the new time
        Apply(workshop, request, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        if (moved && workshop.CancelledAt is null)
            await activity.NotifyWorkshopAsync(workshop.Attendees.Select(a => a.UserId).ToList(), me, workshop.HostId, id,
                ActivityTypes.WorkshopChanged, ct);
        return (await SummariesAsync(db.Workshops.Where(w => w.Id == id), me, ct)).Single();
    }

    /// <summary>Calls a workshop off (it stays listed as cancelled, its chat closes); attendees hear about it.</summary>
    [HttpPost(ApiRoutes.WorkshopCancel)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var workshop = await db.Workshops.Include(w => w.Attendees).SingleOrDefaultAsync(w => w.Id == id, ct);
        if (workshop is null) return NotFound();
        if (workshop.HostId != me && !await admins.IsAdminAsync(me, ct)) return Forbid();
        if (workshop.CancelledAt is not null) return NoContent();

        workshop.CancelledAt = workshop.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await activity.NotifyWorkshopAsync(workshop.Attendees.Select(a => a.UserId).ToList(), me, workshop.HostId, id,
            ActivityTypes.WorkshopCancelled, ct);
        logger.LogInformation("User {UserId} cancelled workshop {WorkshopId}", me, id);
        return NoContent();
    }

    /// <summary>Deletes a workshop outright (host or admin), its chat included.</summary>
    [HttpDelete(ApiRoutes.Workshop)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var host = await db.Workshops.Where(w => w.Id == id).Select(w => (Guid?)w.HostId).SingleOrDefaultAsync(ct);
        if (host is null) return NotFound();
        if (host != me && !await admins.IsAdminAsync(me, ct)) return Forbid();
        await db.Workshops.Where(w => w.Id == id).ExecuteDeleteAsync(ct);
        logger.LogInformation("User {UserId} deleted workshop {WorkshopId}", me, id);
        return NoContent();
    }

    [HttpPut(ApiRoutes.WorkshopAttendance)]
    public async Task<IActionResult> Join(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var workshop = await Visible(me).Where(w => w.Id == id)
            .Select(w => new
            {
                w.HostId, w.EndsAt, w.MaxPeople, Cancelled = w.CancelledAt != null, Count = w.Attendees.Count,
                Going = w.Attendees.Any(a => a.UserId == me),
                Blocked = db.Blocks.Any(b => b.BlockerId == w.HostId && b.BlockedId == me),
            })
            .SingleOrDefaultAsync(ct);
        if (workshop is null) return NotFound();
        if (workshop.Going) return NoContent();
        if (WorkshopRules.CannotJoin(workshop.HostId == me, workshop.Cancelled, workshop.EndsAt, clock.GetUtcNow(), workshop.Count,
                workshop.MaxPeople, workshop.Blocked) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        db.WorkshopAttendees.Add(new WorkshopAttendee { WorkshopId = id, UserId = me, JoinedAt = clock.GetUtcNow() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return NoContent(); /* joined twice at the same moment */ }
        await activity.NotifyAsync(workshop.HostId, me, ActivityTypes.WorkshopJoined, workshopId: id, ct: ct);
        return NoContent();
    }

    /// <summary>Leaves a workshop. Messages already posted stay in the chat.</summary>
    [HttpDelete(ApiRoutes.WorkshopAttendance)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        await db.WorkshopAttendees.Where(a => a.WorkshopId == id && a.UserId == me).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// The chat, oldest first: the newest <see cref="WorkshopLimits.MessagesShown"/>, or with <paramref name="after"/> only
    /// the messages since (what clients poll for). For the host, people going and site admins. Messages by suspended members
    /// or members the viewer blocked are left out.
    /// </summary>
    [HttpGet(ApiRoutes.WorkshopMessages)]
    public async Task<ActionResult<IReadOnlyList<WorkshopMessageDto>>> Messages(Guid id, long? after = null, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var access = await AccessAsync(id, me, ct);
        if (access is null) return NotFound();
        if (!access.Inside && !access.IsAdmin) return Forbid();

        var messages = db.WorkshopMessages.AsNoTracking()
            .Where(m => m.WorkshopId == id && m.Author.SuspendedAt == null && !m.Author.BlockedBy.Any(b => b.BlockerId == me));
        var rows = after is { } since
            ? await messages.Where(m => m.Id > since).OrderBy(m => m.Id).Take(MaxMessagesPerPoll).Select(Row).ToListAsync(ct)
            : (await messages.OrderByDescending(m => m.Id).Take(WorkshopLimits.MessagesShown).Select(Row).ToListAsync(ct))
                .OrderBy(m => m.Id).ToList();
        return rows.Select(r => ToDto(r, access, me)).ToList();
    }

    [HttpPost(ApiRoutes.WorkshopMessages)]
    public async Task<ActionResult<WorkshopMessageDto>> Post(Guid id, PostWorkshopMessageRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var body = request.Body.Trim();
        if (body.Length == 0) return Problem("Write something first.", statusCode: StatusCodes.Status400BadRequest);
        var access = await AccessAsync(id, me, ct);
        if (access is null) return NotFound();
        if (WorkshopRules.CannotChat(access.IsHost, access.IsGoing, access.Cancelled, access.EndsAt, clock.GetUtcNow()) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        var message = new WorkshopMessage { WorkshopId = id, AuthorId = me, Body = body, CreatedAt = clock.GetUtcNow() };
        db.WorkshopMessages.Add(message);
        await db.SaveChangesAsync(ct);
        var row = await db.WorkshopMessages.AsNoTracking().Where(m => m.Id == message.Id).Select(Row).SingleAsync(ct);
        return ToDto(row, access, me);
    }

    /// <summary>Takes a message down: its author, the workshop's host or a site admin.</summary>
    [HttpDelete(ApiRoutes.WorkshopMessage)]
    public async Task<IActionResult> DeleteMessage(Guid id, long messageId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var message = await db.WorkshopMessages.AsNoTracking().Where(m => m.Id == messageId && m.WorkshopId == id)
            .Select(m => new { m.AuthorId, m.Workshop.HostId }).SingleOrDefaultAsync(ct);
        if (message is null) return NotFound();
        if (message.AuthorId != me && message.HostId != me && !await admins.IsAdminAsync(me, ct)) return Forbid();
        await db.WorkshopMessages.Where(m => m.Id == messageId).ExecuteDeleteAsync(ct);
        logger.LogInformation("User {UserId} deleted message {MessageId} in workshop {WorkshopId}", me, messageId, id);
        return NoContent();
    }

    private sealed record MessageRow(long Id, UserSummaryDto Author, string Body, DateTimeOffset CreatedAt);

    private static readonly System.Linq.Expressions.Expression<Func<WorkshopMessage, MessageRow>> Row = m =>
        new MessageRow(m.Id, new UserSummaryDto(m.Author.Id, m.Author.DisplayName, m.Author.AvatarUrl), m.Body, m.CreatedAt);

    private static WorkshopMessageDto ToDto(MessageRow r, ChatAccess access, Guid me) =>
        new(r.Id, r.Author, r.Body, r.CreatedAt, r.Author.Id == access.HostId,
            r.Author.Id == me || access.IsHost || access.IsAdmin);

    private sealed record ChatAccess(Guid HostId, bool IsHost, bool IsGoing, bool Cancelled, DateTimeOffset EndsAt, bool IsAdmin)
    {
        public bool Inside => IsHost || IsGoing;
    }

    /// <summary>Where the caller stands in a workshop they can see, or null if they can't see it.</summary>
    private async Task<ChatAccess?> AccessAsync(Guid id, Guid me, CancellationToken ct)
    {
        var w = await Visible(me).Where(w => w.Id == id)
            .Select(w => new { w.HostId, Going = w.Attendees.Any(a => a.UserId == me), Cancelled = w.CancelledAt != null, w.EndsAt })
            .SingleOrDefaultAsync(ct);
        return w is null ? null : new ChatAccess(w.HostId, w.HostId == me, w.Going, w.Cancelled, w.EndsAt, await admins.IsAdminAsync(me, ct));
    }

    private bool IsValid(SaveWorkshopRequest r, bool isNew, int attendees)
    {
        foreach (var (field, message) in WorkshopRules.CannotSave(r.Title, r.StartsAt, r.EndsAt, r.VideoLink, clock.GetUtcNow(), isNew,
                     attendees, r.MaxPeople))
            ModelState.AddModelError(field, message);
        return ModelState.IsValid;
    }

    private static string? CleanLink(string? link) => string.IsNullOrWhiteSpace(link) ? null : link.Trim();

    private static void Apply(Workshop workshop, SaveWorkshopRequest r, DateTimeOffset now)
    {
        workshop.Title = r.Title.Trim();
        workshop.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        workshop.StartsAt = r.StartsAt.ToUniversalTime();
        workshop.EndsAt = r.EndsAt.ToUniversalTime();
        (workshop.VideoLink, workshop.MaxPeople, workshop.UpdatedAt) = (CleanLink(r.VideoLink), r.MaxPeople, now);
    }

    /// <summary>
    /// Workshops the viewer may see: not by suspended hosts, nor between members where either blocked the other. A club
    /// workshop is for the club's active members only (and its host).
    /// </summary>
    private IQueryable<Workshop> Visible(Guid me) => db.Workshops.AsNoTracking()
        .Where(w => w.Host.SuspendedAt == null
                    && !w.Host.BlockedBy.Any(b => b.BlockerId == me)
                    && !db.Blocks.Any(b => b.BlockerId == w.HostId && b.BlockedId == me))
        .Where(w => w.ClubId == null || w.HostId == me
                    || db.ClubMembers.Any(m => m.ClubId == w.ClubId && m.UserId == me && m.Status == ClubMemberStatus.Active));

    private static async Task<List<WorkshopSummaryDto>> SummariesAsync(IQueryable<Workshop> workshops, Guid me, CancellationToken ct) =>
        (await workshops
            .Select(w => new
            {
                w.Id, w.Title, w.StartsAt, w.EndsAt, w.MaxPeople, w.CancelledAt, w.HostId, HasLink = w.VideoLink != null,
                Host = new UserSummaryDto(w.Host.Id, w.Host.DisplayName, w.Host.AvatarUrl),
                Count = w.Attendees.Count,
                Going = w.Attendees.Any(a => a.UserId == me),
                w.ClubId, ClubName = w.Club == null ? null : w.Club.Name,
            })
            .ToListAsync(ct))
        .Select(r => new WorkshopSummaryDto(r.Id, r.Title, r.StartsAt, r.EndsAt, r.Host, r.Count + 1, r.MaxPeople, r.Going,
            r.HostId == me, r.CancelledAt is not null, r.HasLink, r.ClubId, r.ClubName))
        .ToList();
}
