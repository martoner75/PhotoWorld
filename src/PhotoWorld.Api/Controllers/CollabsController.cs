using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Api.Services.Clubs;
using PhotoWorld.Api.Services.Moderation;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Collabs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// Collabs: a member posts a call for someone to shoot with, others reply, the poster accepts the people they want, and
/// those people get the exact meeting point and a chat. Calls are approved by a site admin before others see them, and
/// don't show between members where either blocked the other.
/// </summary>
[ApiController]
[Authorize]
public class CollabsController(PhotoWorldDbContext db, AdminAccess admins, ActivityFeed activity, ReportService reports,
    SupabaseStorage storage, TimeProvider clock, ILogger<CollabsController> logger) : ControllerBase
{
    private const int PageSize = 24;
    private const int MaxMessagesPerPoll = 200;

    /// <summary>
    /// The board (<see cref="CollabLists.Open"/>: approved calls still to come, soonest first) or the caller's own calls
    /// and replies (<see cref="CollabLists.Mine"/>). Near one of the caller's places (<paramref name="nearPlace"/>, the
    /// website) or a rounded location (<paramref name="lat"/>/<paramref name="lon"/>, the app), only calls within
    /// <paramref name="km"/> are listed, nearest first.
    /// </summary>
    [HttpGet(ApiRoutes.Collabs)]
    public async Task<ActionResult<PagedResult<CollabSummaryDto>>> List(string? list = null, string? role = null, string? terms = null,
        string? q = null, Guid? nearPlace = null, double? lat = null, double? lon = null, double? km = null, int page = 1,
        CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        page = Math.Max(1, page);
        var now = clock.GetUtcNow();
        var query = Visible(me, reviewer: false);

        if (list == CollabLists.Mine)
        {
            query = query.Where(c => c.PosterId == me || c.Replies.Any(r => r.UserId == me));
            var mineTotal = await query.CountAsync(ct);
            var mine = await SummariesAsync(query.OrderByDescending(c => c.StartsAt).Skip((page - 1) * PageSize).Take(PageSize), me, ct);
            return new PagedResult<CollabSummaryDto>(mine.Select(r => r.Dto).ToList(), page, PageSize, mineTotal);
        }

        query = query.Where(c => c.ReviewStatus == CollabReviewStatus.Approved && c.CancelledAt == null && c.StartsAt > now);
        if (role is not null && CollabRoles.All.Contains(role)) query = query.Where(c => c.Role == role);
        if (terms is not null && CollabTerms.All.Contains(terms)) query = query.Where(c => c.Terms == terms);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(term) || c.Area.ToLower().Contains(term)
                                     || (c.Description != null && c.Description.ToLower().Contains(term)));
        }

        if (nearPlace is { } placeId)
        {
            var place = await db.Places.AsNoTracking().Where(p => p.Id == placeId && p.UserId == me)
                .Select(p => new { p.Latitude, p.Longitude }).SingleOrDefaultAsync(ct);
            if (place is null) return NotFound();
            (lat, lon) = (place.Latitude, place.Longitude);
        }
        if (lat is { } fromLat && lon is { } fromLon && double.IsFinite(fromLat) && double.IsFinite(fromLon))
        {
            var radius = Math.Clamp(km ?? CollabLimits.DefaultNearKm, 1, CollabLimits.MaxNearKm);
            // The latitude box keeps it to the region; the exact distance is measured in memory.
            var latSpan = radius / 111.0;
            var nearby = (await SummariesAsync(query.Where(c => c.AreaLatitude >= fromLat - latSpan && c.AreaLatitude <= fromLat + latSpan), me, ct))
                .Select(r => r.Dto with { DistanceKm = Math.Round(ClubRules.DistanceKm(fromLat, fromLon, r.Latitude, r.Longitude), 1) })
                .Where(s => s.DistanceKm <= radius)
                .OrderBy(s => s.DistanceKm).ThenBy(s => s.StartsAt)
                .ToList();
            return new PagedResult<CollabSummaryDto>(nearby.Skip((page - 1) * PageSize).Take(PageSize).ToList(), page, PageSize, nearby.Count);
        }

        var total = await query.CountAsync(ct);
        var items = await SummariesAsync(query.OrderBy(c => c.StartsAt).Skip((page - 1) * PageSize).Take(PageSize), me, ct);
        return new PagedResult<CollabSummaryDto>(items.Select(r => r.Dto).ToList(), page, PageSize, total);
    }

    [HttpGet(ApiRoutes.Collab)]
    public async Task<ActionResult<CollabDto>> Get(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var isAdmin = await admins.IsAdminAsync(me, ct);
        var summary = (await SummariesAsync(Visible(me, isAdmin).Where(c => c.Id == id), me, ct)).SingleOrDefault()?.Dto;
        if (summary is null) return NotFound();

        var details = await db.Collabs.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new
            {
                c.Description, c.TermsNote, c.ReviewNote, c.MeetingPointName, c.MeetingLatitude, c.MeetingLongitude,
                c.AreaLatitude, c.AreaLongitude,
                BlockedByPoster = db.Blocks.Any(b => b.BlockerId == c.PosterId && b.BlockedId == me),
            })
            .SingleAsync(ct);

        var replies = await ReplyRows(id, me).ToListAsync(ct);
        var mine = replies.SingleOrDefault(r => r.Member.Id == me);
        var accepted = replies.Where(r => r.Status == CollabReplyStatus.Accepted).ToList();
        var isPoster = summary.IsPoster;
        var isAccepted = mine?.Status == CollabReplyStatus.Accepted;
        var inside = isPoster || isAccepted;
        var now = clock.GetUtcNow();

        var replyReason = CollabRules.CannotReply(isPoster, summary.ReviewStatus == CollabReviewStatus.Approved, summary.IsCancelled,
            summary.StartsAt, now, details.BlockedByPoster);
        // Changing a reply is only possible while the poster hasn't decided on it.
        var canReply = replyReason is null && (mine is null || mine.Status == CollabReplyStatus.Pending);
        var chatReason = CollabRules.CannotChat(isPoster, isAccepted, summary.IsCancelled, summary.EndsAt, now);
        var canConfirm = isPoster
            ? accepted.Any(r => !r.PosterConfirmed) && ConfirmReason(true, true, summary, now) is null
            : isAccepted && !mine!.MemberConfirmed && ConfirmReason(true, true, summary, now) is null;

        var order = await db.CollabPhotos.Where(p => p.CollabId == id).OrderBy(p => p.Position).Select(p => p.PhotoId).ToListAsync(ct);
        var photos = (await db.Photos.Where(p => order.Contains(p.Id)).Where(Moderation.PhotoVisibleTo(me)).ToSummariesAsync(me, storage, ct))
            .OrderBy(p => order.IndexOf(p.Id)).ToList();

        return new CollabDto(
            summary,
            details.Description,
            details.TermsNote,
            CollabLimits.TagFor(id),
            details.AreaLatitude,
            details.AreaLongitude,
            photos,
            inside || isAdmin ? new CollabMeetingPointDto(details.MeetingPointName, details.MeetingLatitude, details.MeetingLongitude) : null,
            isPoster || isAdmin ? replies.Select(r => r.ToDto()).ToList() : [],
            mine?.ToDto(),
            inside || isAdmin ? accepted.Select(r => r.Member).ToList() : [],
            isPoster ? details.ReviewNote : null,
            CanEdit: isPoster || isAdmin,
            CanReply: canReply,
            CannotReplyReason: canReply || isPoster ? null : mine is not null ? null : replyReason,
            CanReadChat: inside || isAdmin,
            CanChat: chatReason is null,
            CannotChatReason: inside ? chatReason : null,
            CanConfirm: canConfirm,
            CanReview: isAdmin && !isPoster && summary.ReviewStatus != CollabReviewStatus.Rejected);
    }

    /// <summary>
    /// Posts a call. It waits for a site admin's approval (a site admin's own is approved straight away); once approved,
    /// the poster's followers hear about it.
    /// </summary>
    [HttpPost(ApiRoutes.Collabs)]
    public async Task<ActionResult<CollabSummaryDto>> Create(SaveCollabRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!IsValid(request, isNew: true)) return ValidationProblem(ModelState);
        var now = clock.GetUtcNow();
        var open = await db.Collabs.CountAsync(c => c.PosterId == me && c.CancelledAt == null && c.EndsAt > now
                                                    && c.ReviewStatus != CollabReviewStatus.Rejected, ct);
        if (CollabRules.CannotPost(open) is { } tooMany) return Problem(tooMany, statusCode: StatusCodes.Status400BadRequest);

        var place = await OwnPlaceAsync(request.PlaceId, me, ct);
        if (place is null) return ValidationProblemFor("placeId", "Pick one of your places as the meeting point.");
        var photoIds = await OwnPhotosAsync(request.PhotoIds, me, ct);
        if (photoIds is null) return ValidationProblemFor("photoIds", "Mood photos must be your own photos.");

        var isAdmin = await admins.IsAdminAsync(me, ct);
        var collab = new Collab
        {
            PosterId = me, Title = "", Role = "", Terms = "", Area = "", MeetingPointName = "", CreatedAt = now,
            ReviewStatus = isAdmin ? CollabReviewStatus.Approved : CollabReviewStatus.Pending,
            ReviewedAt = isAdmin ? now : null,
        };
        Apply(collab, request, place, now);
        collab.Photos = photoIds.Select((p, i) => new CollabPhoto { PhotoId = p, Position = i }).ToList();
        db.Collabs.Add(collab);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} posted collab {CollabId} ({Review})", me, collab.Id, collab.ReviewStatus);
        if (collab.ReviewStatus == CollabReviewStatus.Approved) await AnnounceAsync(collab.Id, me, ct);
        return CreatedAtAction(nameof(Get), new { id = collab.Id },
            (await SummariesAsync(db.Collabs.Where(c => c.Id == collab.Id), me, ct)).Single().Dto);
    }

    /// <summary>
    /// Changes a call (its poster only: the meeting point and photos come from their own places and photos; site admins
    /// cancel, delete or take down instead). Like blog posts, an approved call isn't reviewed again for edits;
    /// reports still cover it. The people accepted hear when the time or the meeting point changes.
    /// </summary>
    [HttpPut(ApiRoutes.Collab)]
    public async Task<ActionResult<CollabSummaryDto>> Update(Guid id, SaveCollabRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var collab = await db.Collabs.Include(c => c.Photos).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (collab is null) return NotFound();
        if (collab.PosterId != me) return Forbid();
        if (!IsValid(request, isNew: false)) return ValidationProblem(ModelState);

        var place = await OwnPlaceAsync(request.PlaceId, me, ct);
        if (place is null) return ValidationProblemFor("placeId", "Pick one of your places as the meeting point.");
        var photoIds = await OwnPhotosAsync(request.PhotoIds, me, ct);
        if (photoIds is null) return ValidationProblemFor("photoIds", "Mood photos must be your own photos.");

        var moved = collab.StartsAt != request.StartsAt || collab.EndsAt != request.EndsAt
                    || collab.MeetingLatitude != place.Latitude || collab.MeetingLongitude != place.Longitude;
        if (collab.StartsAt != request.StartsAt) collab.ReminderSentAt = null; // remind again for the new time
        if (collab.EndsAt != request.EndsAt) collab.ConfirmRequestSentAt = null;
        Apply(collab, request, place, clock.GetUtcNow());
        db.CollabPhotos.RemoveRange(collab.Photos);
        collab.Photos = photoIds.Select((p, i) => new CollabPhoto { CollabId = id, PhotoId = p, Position = i }).ToList();
        await db.SaveChangesAsync(ct);

        if (moved && collab.CancelledAt is null)
            await activity.NotifyCollabAsync(await AcceptedAsync(id, ct), me, collab.PosterId, id, ActivityTypes.CollabChanged, ct);
        return (await SummariesAsync(db.Collabs.Where(c => c.Id == id), me, ct)).Single().Dto;
    }

    /// <summary>Calls a collab off (it stays visible as cancelled, its chat closes); everyone who replied hears about it.</summary>
    [HttpPost(ApiRoutes.CollabCancel)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var collab = await db.Collabs.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (collab is null) return NotFound();
        if (collab.PosterId != me && !await admins.IsAdminAsync(me, ct)) return Forbid();
        if (collab.CancelledAt is not null) return NoContent();

        collab.CancelledAt = collab.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        var repliers = await db.CollabReplies.Where(r => r.CollabId == id && r.Status != CollabReplyStatus.Declined)
            .Select(r => r.UserId).ToListAsync(ct);
        await activity.NotifyCollabAsync(repliers, me, collab.PosterId, id, ActivityTypes.CollabCancelled, ct);
        logger.LogInformation("User {UserId} cancelled collab {CollabId}", me, id);
        return NoContent();
    }

    /// <summary>Deletes a call outright (the poster or a site admin), its replies and chat included.</summary>
    [HttpDelete(ApiRoutes.Collab)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var poster = await db.Collabs.Where(c => c.Id == id).Select(c => (Guid?)c.PosterId).SingleOrDefaultAsync(ct);
        if (poster is null) return NotFound();
        if (poster != me && !await admins.IsAdminAsync(me, ct)) return Forbid();
        await db.Collabs.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
        logger.LogInformation("User {UserId} deleted collab {CollabId}", me, id);
        return NoContent();
    }

    /// <summary>Replies to a call, or changes a reply the poster hasn't decided on yet. The poster hears about the first one.</summary>
    [HttpPut(ApiRoutes.CollabReply)]
    public async Task<IActionResult> Reply(Guid id, ReplyToCollabRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var message = request.Message.Trim();
        if (message.Length == 0) return Problem("Write a few words about yourself first.", statusCode: StatusCodes.Status400BadRequest);
        var collab = await Visible(me, reviewer: false).Where(c => c.Id == id)
            .Select(c => new
            {
                c.PosterId, c.StartsAt, Cancelled = c.CancelledAt != null, Approved = c.ReviewStatus == CollabReviewStatus.Approved,
                Blocked = db.Blocks.Any(b => b.BlockerId == c.PosterId && b.BlockedId == me),
            })
            .SingleOrDefaultAsync(ct);
        if (collab is null) return NotFound();
        var now = clock.GetUtcNow();
        if (CollabRules.CannotReply(collab.PosterId == me, collab.Approved, collab.Cancelled, collab.StartsAt, now, collab.Blocked) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        var reply = await db.CollabReplies.SingleOrDefaultAsync(r => r.CollabId == id && r.UserId == me, ct);
        if (reply is null)
        {
            db.CollabReplies.Add(new CollabReply
            {
                CollabId = id, UserId = me, Message = message, SharePortfolio = request.SharePortfolio, CreatedAt = now, UpdatedAt = now,
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return NoContent(); /* replied twice at the same moment */ }
            await activity.NotifyAsync(collab.PosterId, me, ActivityTypes.CollabReply, snippet: message, collabId: id, ct: ct);
            return NoContent();
        }

        if (reply.Status != CollabReplyStatus.Pending)
            return Problem("The poster has already answered your reply.", statusCode: StatusCodes.Status409Conflict);
        (reply.Message, reply.SharePortfolio, reply.UpdatedAt) = (message, request.SharePortfolio, now);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Withdraws the caller's reply (accepted or not). Messages already posted stay in the chat.</summary>
    [HttpDelete(ApiRoutes.CollabReply)]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        await db.CollabReplies.Where(r => r.CollabId == id && r.UserId == me).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    /// <summary>The poster accepts a reply: that member gets the meeting point and joins the chat.</summary>
    [HttpPost(ApiRoutes.CollabReplyAccept)]
    public async Task<IActionResult> Accept(Guid id, Guid userId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var collab = await db.Collabs.AsNoTracking().Where(c => c.Id == id && c.PosterId == me)
            .Select(c => new
            {
                c.EndsAt, Cancelled = c.CancelledAt != null,
                Accepted = c.Replies.Count(r => r.Status == CollabReplyStatus.Accepted),
            })
            .SingleOrDefaultAsync(ct);
        if (collab is null) return NotFound();
        var reply = await db.CollabReplies.SingleOrDefaultAsync(r => r.CollabId == id && r.UserId == userId, ct);
        if (reply is null) return NotFound();
        if (reply.Status == CollabReplyStatus.Accepted) return NoContent();
        if (CollabRules.CannotAccept(collab.Cancelled, collab.EndsAt, clock.GetUtcNow(), collab.Accepted) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        (reply.Status, reply.DecidedAt) = (CollabReplyStatus.Accepted, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} accepted {MemberId} on collab {CollabId}", me, userId, id);
        await activity.NotifyAsync(userId, me, ActivityTypes.CollabAccepted, collabId: id, ct: ct);
        return NoContent();
    }

    /// <summary>The poster declines a reply (also after accepting it). The member isn't notified; their reply shows it.</summary>
    [HttpPost(ApiRoutes.CollabReplyDecline)]
    public async Task<IActionResult> Decline(Guid id, Guid userId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!await db.Collabs.AnyAsync(c => c.Id == id && c.PosterId == me, ct)) return NotFound();
        var changed = await db.CollabReplies.Where(r => r.CollabId == id && r.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, CollabReplyStatus.Declined)
                .SetProperty(r => r.DecidedAt, clock.GetUtcNow())
                .SetProperty(r => r.PosterConfirmedAt, (DateTimeOffset?)null)
                .SetProperty(r => r.MemberConfirmedAt, (DateTimeOffset?)null), ct);
        return changed == 0 ? NotFound() : NoContent();
    }

    /// <summary>
    /// Says the shoot happened: the poster for someone they accepted, or that member for themselves. Once both have, it
    /// counts as a confirmed collab on both profiles.
    /// </summary>
    [HttpPut(ApiRoutes.CollabReplyConfirmation)]
    public async Task<IActionResult> Confirm(Guid id, Guid userId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var reply = await db.CollabReplies.Include(r => r.Collab).SingleOrDefaultAsync(r => r.CollabId == id && r.UserId == userId, ct);
        if (reply is null) return NotFound();
        var c = reply.Collab;
        var isPoster = c.PosterId == me;
        if (!isPoster && userId != me) return NotFound();
        if (CollabRules.CannotConfirm(true, reply.Status == CollabReplyStatus.Accepted, c.CancelledAt is not null, c.StartsAt, c.EndsAt,
                clock.GetUtcNow()) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        var now = clock.GetUtcNow();
        if (isPoster) reply.PosterConfirmedAt ??= now;
        else reply.MemberConfirmedAt ??= now;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// The chat, oldest first: the newest <see cref="CollabLimits.MessagesShown"/>, or with <paramref name="after"/> only the
    /// messages since. For the poster, the people accepted and site admins.
    /// </summary>
    [HttpGet(ApiRoutes.CollabMessages)]
    public async Task<ActionResult<IReadOnlyList<CollabMessageDto>>> Messages(Guid id, long? after = null, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var access = await AccessAsync(id, me, ct);
        if (access is null) return NotFound();
        if (!access.Inside && !access.IsAdmin) return Forbid();

        var messages = db.CollabMessages.AsNoTracking()
            .Where(m => m.CollabId == id && m.Author.SuspendedAt == null && !m.Author.BlockedBy.Any(b => b.BlockerId == me));
        var rows = after is { } since
            ? await messages.Where(m => m.Id > since).OrderBy(m => m.Id).Take(MaxMessagesPerPoll).Select(Row).ToListAsync(ct)
            : (await messages.OrderByDescending(m => m.Id).Take(CollabLimits.MessagesShown).Select(Row).ToListAsync(ct))
                .OrderBy(m => m.Id).ToList();
        return rows.Select(r => ToDto(r, access, me)).ToList();
    }

    [HttpPost(ApiRoutes.CollabMessages)]
    public async Task<ActionResult<CollabMessageDto>> Post(Guid id, PostCollabMessageRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var body = request.Body.Trim();
        if (body.Length == 0) return Problem("Write something first.", statusCode: StatusCodes.Status400BadRequest);
        var access = await AccessAsync(id, me, ct);
        if (access is null) return NotFound();
        if (CollabRules.CannotChat(access.IsPoster, access.IsAccepted, access.Cancelled, access.EndsAt, clock.GetUtcNow()) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        var message = new CollabMessage { CollabId = id, AuthorId = me, Body = body, CreatedAt = clock.GetUtcNow() };
        db.CollabMessages.Add(message);
        await db.SaveChangesAsync(ct);
        var row = await db.CollabMessages.AsNoTracking().Where(m => m.Id == message.Id).Select(Row).SingleAsync(ct);
        return ToDto(row, access, me);
    }

    /// <summary>Takes a message down: its author, the poster or a site admin.</summary>
    [HttpDelete(ApiRoutes.CollabMessage)]
    public async Task<IActionResult> DeleteMessage(Guid id, long messageId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var message = await db.CollabMessages.AsNoTracking().Where(m => m.Id == messageId && m.CollabId == id)
            .Select(m => new { m.AuthorId, m.Collab.PosterId }).SingleOrDefaultAsync(ct);
        if (message is null) return NotFound();
        if (message.AuthorId != me && message.PosterId != me && !await admins.IsAdminAsync(me, ct)) return Forbid();
        await db.CollabMessages.Where(m => m.Id == messageId).ExecuteDeleteAsync(ct);
        logger.LogInformation("User {UserId} deleted message {MessageId} in collab {CollabId}", me, messageId, id);
        return NoContent();
    }

    [HttpPost(ApiRoutes.CollabReports)]
    public async Task<IActionResult> Report(Guid id, CreateReportRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var posterId = await Visible(me, reviewer: false).Where(c => c.Id == id).Select(c => (Guid?)c.PosterId).SingleOrDefaultAsync(ct);
        if (posterId is null) return NotFound();
        if (posterId == me) return Problem("You can't report your own call.", statusCode: StatusCodes.Status400BadRequest);
        return this.ToResponse(await reports.SaveAsync(ReportTarget.Collab(id), me, request, ct));
    }

    /// <summary>Site admins: calls waiting for approval, longest-waiting first.</summary>
    [HttpGet(ApiRoutes.AdminPendingCollabs)]
    public async Task<ActionResult<PagedResult<CollabSummaryDto>>> Pending(int page = 1, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me || !await admins.IsAdminAsync(me, ct)) return Forbid();
        page = Math.Max(1, page);
        var query = db.Collabs.AsNoTracking().Where(c => c.ReviewStatus == CollabReviewStatus.Pending && c.Poster.SuspendedAt == null
                                                         && c.CancelledAt == null);
        var total = await query.CountAsync(ct);
        var items = await SummariesAsync(query.OrderBy(c => c.CreatedAt).Skip((page - 1) * PageSize).Take(PageSize), me, ct);
        return new PagedResult<CollabSummaryDto>(items.Select(r => r.Dto).ToList(), page, PageSize, total);
    }

    [HttpPost(ApiRoutes.AdminCollabApproval)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me || !await admins.IsAdminAsync(me, ct)) return Forbid();
        var collab = await db.Collabs.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (collab is null) return NotFound();
        if (collab.ReviewStatus != CollabReviewStatus.Pending)
            return Problem("This call isn't waiting for approval any more.", statusCode: StatusCodes.Status409Conflict);

        (collab.ReviewStatus, collab.ReviewedAt, collab.ReviewNote) = (CollabReviewStatus.Approved, clock.GetUtcNow(), null);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} approved collab {CollabId}", me, id);
        await activity.NotifyAsync(collab.PosterId, me, ActivityTypes.CollabApproved, collabId: id, ct: ct);
        await AnnounceAsync(id, collab.PosterId, ct);
        return NoContent();
    }

    /// <summary>Site admins: turns down a pending call, or takes down an approved one. It stays visible to its poster only.</summary>
    [HttpPost(ApiRoutes.AdminCollabRejection)]
    public async Task<IActionResult> Reject(Guid id, RejectCollabRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me || !await admins.IsAdminAsync(me, ct)) return Forbid();
        var collab = await db.Collabs.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (collab is null) return NotFound();
        if (collab.ReviewStatus == CollabReviewStatus.Rejected) return NoContent();

        collab.ReviewStatus = CollabReviewStatus.Rejected;
        collab.ReviewedAt = clock.GetUtcNow();
        collab.ReviewNote = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} rejected collab {CollabId}", me, id);
        await activity.NotifyAsync(collab.PosterId, me, ActivityTypes.CollabRejected, collabId: id, snippet: collab.ReviewNote, ct: ct);
        return NoContent();
    }

    /// <summary>Tells the poster's followers about a call once it's on the board.</summary>
    private async Task AnnounceAsync(Guid id, Guid posterId, CancellationToken ct)
    {
        var followers = await db.Follows.Where(f => f.FolloweeId == posterId).Select(f => f.FollowerId).ToListAsync(ct);
        await activity.NotifyCollabAsync(followers, posterId, posterId, id, ActivityTypes.CollabNew, ct);
    }

    private Task<List<Guid>> AcceptedAsync(Guid id, CancellationToken ct) =>
        db.CollabReplies.Where(r => r.CollabId == id && r.Status == CollabReplyStatus.Accepted).Select(r => r.UserId).ToListAsync(ct);

    private static string? ConfirmReason(bool isPosterOrMember, bool accepted, CollabSummaryDto s, DateTimeOffset now) =>
        CollabRules.CannotConfirm(isPosterOrMember, accepted, s.IsCancelled, s.StartsAt, s.EndsAt, now);

    private sealed record PlaceRow(string Name, double Latitude, double Longitude);

    private Task<PlaceRow?> OwnPlaceAsync(Guid placeId, Guid ownerId, CancellationToken ct) =>
        db.Places.AsNoTracking().Where(p => p.Id == placeId && p.UserId == ownerId)
            .Select(p => new PlaceRow(p.Name, p.Latitude, p.Longitude)).SingleOrDefaultAsync(ct);

    /// <summary>The requested photos in order, if they're all the owner's own (and not rejected); null otherwise.</summary>
    private async Task<List<Guid>?> OwnPhotosAsync(IReadOnlyList<Guid>? ids, Guid ownerId, CancellationToken ct)
    {
        var wanted = (ids ?? []).Distinct().ToList();
        if (wanted.Count == 0) return [];
        var found = await db.Photos.Where(p => wanted.Contains(p.Id) && p.OwnerId == ownerId && p.ReviewStatus != PhotoReviewStatus.Rejected)
            .CountAsync(ct);
        return found == wanted.Count ? wanted : null;
    }

    private bool IsValid(SaveCollabRequest r, bool isNew)
    {
        foreach (var (field, message) in CollabRules.CannotSave(r.Title, r.Role, r.Terms, r.Area, r.StartsAt, r.EndsAt, clock.GetUtcNow(),
                     isNew, r.PhotoIds?.Distinct().Count() ?? 0))
            ModelState.AddModelError(field, message);
        return ModelState.IsValid;
    }

    private ActionResult ValidationProblemFor(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static void Apply(Collab c, SaveCollabRequest r, PlaceRow place, DateTimeOffset now)
    {
        c.Title = r.Title.Trim();
        (c.Role, c.Terms, c.TermsNote, c.Description) = (r.Role, r.Terms, Clean(r.TermsNote), Clean(r.Description));
        c.StartsAt = r.StartsAt.ToUniversalTime();
        c.EndsAt = r.EndsAt.ToUniversalTime();
        c.Area = r.Area.Trim();
        // Everyone sees the area at about 1 km; the exact place goes only to the people accepted.
        c.AreaLatitude = ClubRules.Area(place.Latitude)!.Value;
        c.AreaLongitude = ClubRules.Area(place.Longitude)!.Value;
        (c.MeetingPointName, c.MeetingLatitude, c.MeetingLongitude) = (place.Name, place.Latitude, place.Longitude);
        c.UpdatedAt = now;
    }

    /// <summary>
    /// Calls the viewer may see: <see cref="Moderation.CollabVisibleTo"/>, and not by a member who blocked the viewer.
    /// </summary>
    private IQueryable<Collab> Visible(Guid me, bool reviewer) => db.Collabs.AsNoTracking()
        .Where(Moderation.CollabVisibleTo(me, reviewer))
        .Where(c => c.PosterId == me || !db.Blocks.Any(b => b.BlockerId == c.PosterId && b.BlockedId == me));

    private sealed record SummaryRow(CollabSummaryDto Dto, double Latitude, double Longitude);

    private async Task<List<SummaryRow>> SummariesAsync(IQueryable<Collab> collabs, Guid me, CancellationToken ct)
    {
        var rows = await collabs
            .Select(c => new
            {
                c.Id, c.Title, c.Role, c.Terms, c.StartsAt, c.EndsAt, c.Area, c.PosterId, c.CancelledAt, c.ReviewStatus,
                c.AreaLatitude, c.AreaLongitude,
                Poster = new UserSummaryDto(c.Poster.Id, c.Poster.DisplayName, c.Poster.AvatarUrl),
                Confirmed = db.CollabReplies.Count(r => r.Status == CollabReplyStatus.Accepted && r.PosterConfirmedAt != null
                                                        && r.MemberConfirmedAt != null
                                                        && (r.UserId == c.PosterId || r.Collab.PosterId == c.PosterId)),
                Replies = c.Replies.Count,
                Mine = c.Replies.Where(r => r.UserId == me).Select(r => r.Status).FirstOrDefault(),
                Cover = c.Photos.Where(p => p.Photo.ReviewStatus == PhotoReviewStatus.Approved || c.PosterId == me)
                    .OrderBy(p => p.Position).Select(p => p.Photo.ThumbnailPath).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return rows.Select(r => new SummaryRow(
            new CollabSummaryDto(r.Id, r.Title, r.Role, r.Terms, r.StartsAt, r.EndsAt, r.Area, r.Poster, r.Confirmed, r.Replies,
                r.Cover is null ? null : storage.ThumbnailUrl(r.Cover), null, r.PosterId == me, r.Mine, r.CancelledAt is not null,
                r.ReviewStatus),
            r.AreaLatitude, r.AreaLongitude)).ToList();
    }

    private sealed record ReplyRow(UserSummaryDto Member, string Message, string Status, DateTimeOffset CreatedAt, int ConfirmedCount,
        string? PortfolioSlug, bool PosterConfirmed, bool MemberConfirmed)
    {
        public CollabReplyDto ToDto() => new(Member, Message, Status, CreatedAt, ConfirmedCount, PortfolioSlug, PosterConfirmed, MemberConfirmed);
    }

    /// <summary>A call's replies the viewer may see (not from suspended members or ones they blocked), oldest first.</summary>
    private IQueryable<ReplyRow> ReplyRows(Guid id, Guid me) => db.CollabReplies.AsNoTracking()
        .Where(r => r.CollabId == id && r.User.SuspendedAt == null && !r.User.BlockedBy.Any(b => b.BlockerId == me))
        .OrderBy(r => r.CreatedAt)
        .Select(r => new ReplyRow(
            new UserSummaryDto(r.User.Id, r.User.DisplayName, r.User.AvatarUrl),
            r.Message, r.Status, r.CreatedAt,
            db.CollabReplies.Count(x => x.Status == CollabReplyStatus.Accepted && x.PosterConfirmedAt != null && x.MemberConfirmedAt != null
                                        && (x.UserId == r.UserId || x.Collab.PosterId == r.UserId)),
            r.SharePortfolio
                ? db.Portfolios.Where(p => p.UserId == r.UserId && p.IsPublic).Select(p => p.Slug).FirstOrDefault()
                : null,
            r.PosterConfirmedAt != null, r.MemberConfirmedAt != null));

    private sealed record MessageRow(long Id, UserSummaryDto Author, string Body, DateTimeOffset CreatedAt);

    private static readonly Expression<Func<CollabMessage, MessageRow>> Row = m =>
        new MessageRow(m.Id, new UserSummaryDto(m.Author.Id, m.Author.DisplayName, m.Author.AvatarUrl), m.Body, m.CreatedAt);

    private static CollabMessageDto ToDto(MessageRow r, ChatAccess access, Guid me) =>
        new(r.Id, r.Author, r.Body, r.CreatedAt, r.Author.Id == access.PosterId, r.Author.Id == me || access.IsPoster || access.IsAdmin);

    private sealed record ChatAccess(Guid PosterId, bool IsPoster, bool IsAccepted, bool Cancelled, DateTimeOffset EndsAt, bool IsAdmin)
    {
        public bool Inside => IsPoster || IsAccepted;
    }

    /// <summary>Where the caller stands in a call they can see, or null if they can't see it.</summary>
    private async Task<ChatAccess?> AccessAsync(Guid id, Guid me, CancellationToken ct)
    {
        var isAdmin = await admins.IsAdminAsync(me, ct);
        var c = await Visible(me, isAdmin).Where(c => c.Id == id)
            .Select(c => new
            {
                c.PosterId, Cancelled = c.CancelledAt != null, c.EndsAt,
                Accepted = c.Replies.Any(r => r.UserId == me && r.Status == CollabReplyStatus.Accepted),
            })
            .SingleOrDefaultAsync(ct);
        return c is null ? null : new ChatAccess(c.PosterId, c.PosterId == me, c.Accepted, c.Cancelled, c.EndsAt, isAdmin);
    }
}
