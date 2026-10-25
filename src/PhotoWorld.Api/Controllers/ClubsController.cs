using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Api.Services.Clubs;
using PhotoWorld.Api.Services.Moderation;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// Camera clubs: the directory, a member's clubs, running a club (owner and admins) and joining one. A club the
/// viewer can't see (waiting for approval, hidden by moderation, owned by someone they blocked) answers 404.
/// </summary>
[ApiController]
[Authorize]
public class ClubsController(PhotoWorldDbContext db, ActivityFeed activity, AdminAccess admins, ReportService reports,
    TimeProvider clock, ILogger<ClubsController> logger) : ControllerBase
{
    private const int PageSize = 24;

    /// <summary>
    /// Approved clubs, by name or city (<paramref name="q"/>). With a starting point (one of the caller's places, or
    /// <paramref name="lat"/>/<paramref name="lon"/> from the app) only clubs within <see cref="ClubLimits.NearKm"/>
    /// are listed, nearest first; otherwise biggest first.
    /// </summary>
    [HttpGet(ApiRoutes.Clubs)]
    public async Task<ActionResult<PagedResult<ClubSummaryDto>>> List(string? q = null, Guid? nearPlace = null,
        double? lat = null, double? lon = null, int page = 1, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        page = Math.Max(1, page);
        var query = Visible(me, reviewer: false).Where(c => c.ReviewStatus == ClubReviewStatus.Approved);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term) || (c.City != null && c.City.ToLower().Contains(term)));
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
            // Few clubs have an area, so measuring in memory is fine; the box keeps it to the region.
            var latSpan = ClubLimits.NearKm / 111.0;
            var nearby = (await SummariesAsync(query.Where(c => c.Latitude != null
                    && c.Latitude >= fromLat - latSpan && c.Latitude <= fromLat + latSpan), me, ct))
                .Select(s => s.Club with { DistanceKm = Math.Round(ClubRules.DistanceKm(fromLat, fromLon, s.Latitude!.Value, s.Longitude!.Value), 1) })
                .Where(s => s.DistanceKm <= ClubLimits.NearKm)
                .OrderBy(s => s.DistanceKm)
                .ToList();
            return new PagedResult<ClubSummaryDto>(nearby.Skip((page - 1) * PageSize).Take(PageSize).ToList(), page, PageSize, nearby.Count);
        }

        var total = await query.CountAsync(ct);
        var ordered = query
            .OrderByDescending(c => c.Members.Count(m => m.Status == ClubMemberStatus.Active))
            .ThenBy(c => c.Name)
            .Skip((page - 1) * PageSize).Take(PageSize);
        var items = (await SummariesAsync(ordered, me, ct)).Select(s => s.Club).ToList();
        return new PagedResult<ClubSummaryDto>(items, page, PageSize, total);
    }

    /// <summary>Clubs the caller owns, runs, belongs to or asked to join (their own pending ones included).</summary>
    [HttpGet(ApiRoutes.MyClubs)]
    public async Task<ActionResult<IReadOnlyList<ClubSummaryDto>>> Mine(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var query = Visible(me, reviewer: false).Where(c => c.Members.Any(m => m.UserId == me)).OrderBy(c => c.Name);
        return (await SummariesAsync(query, me, ct)).Select(s => s.Club).ToList();
    }

    [HttpGet(ApiRoutes.Club)]
    public async Task<ActionResult<ClubDto>> Get(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var dto = await LoadAsync(id, me, await admins.IsAdminAsync(me, ct), ct);
        return dto is null ? NotFound() : dto;
    }

    /// <summary>Creates a club owned by the caller. It waits for a site admin's approval unless the caller is one.</summary>
    [HttpPost(ApiRoutes.Clubs)]
    public async Task<ActionResult<ClubDto>> Create(SaveClubRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!Validate(request)) return ValidationProblem(ModelState);
        if (await db.Clubs.CountAsync(c => c.OwnerId == me, ct) >= ClubLimits.MaxOwned)
            return Problem($"You can run up to {ClubLimits.MaxOwned} clubs.", statusCode: StatusCodes.Status400BadRequest);
        if (await db.ClubMembers.CountAsync(m => m.UserId == me, ct) >= ClubLimits.MaxJoined)
            return Problem($"You can be in up to {ClubLimits.MaxJoined} clubs. Leave one first.", statusCode: StatusCodes.Status400BadRequest);

        var isAdmin = await admins.IsAdminAsync(me, ct);
        var now = clock.GetUtcNow();
        var club = new Club
        {
            OwnerId = me, Name = "", CreatedAt = now,
            ReviewStatus = isAdmin ? ClubReviewStatus.Approved : ClubReviewStatus.Pending,
            ReviewedAt = isAdmin ? now : null,
        };
        Apply(club, request, now);
        club.Members.Add(new ClubMember { UserId = me, Role = ClubRoles.Owner, Status = ClubMemberStatus.Active, JoinedAt = now });
        db.Clubs.Add(club);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} created club {ClubId} ({Review})", me, club.Id, club.ReviewStatus);
        return CreatedAtAction(nameof(Get), new { id = club.Id }, await LoadAsync(club.Id, me, isAdmin, ct));
    }

    [HttpPut(ApiRoutes.Club)]
    public async Task<ActionResult<ClubDto>> Update(Guid id, SaveClubRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var club = await db.Clubs.Where(Moderation.ClubVisibleTo(me)).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (club is null) return NotFound();
        if (!ClubRules.CanManage(await RoleAsync(id, me, ct))) return Forbid();
        if (!Validate(request)) return ValidationProblem(ModelState);

        Apply(club, request, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return (await LoadAsync(id, me, await admins.IsAdminAsync(me, ct), ct))!;
    }

    /// <summary>Only the owner deletes a club; its memberships, reports and activity go with it.</summary>
    [HttpDelete(ApiRoutes.Club)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var deleted = await db.Clubs.Where(c => c.Id == id && c.OwnerId == me).ExecuteDeleteAsync(ct);
        if (deleted == 0) return NotFound();
        logger.LogInformation("User {UserId} deleted club {ClubId}", me, id);
        return NoContent();
    }

    /// <summary>Joins an open club, or asks to join one that approves requests (its owner and admins are told).</summary>
    [HttpPut(ApiRoutes.ClubMembership)]
    public async Task<ActionResult<ClubDto>> Join(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var club = await Visible(me, reviewer: false).Where(c => c.Id == id && c.ReviewStatus == ClubReviewStatus.Approved)
            .Select(c => new
            {
                c.JoinPolicy, c.OwnerId,
                AlreadyIn = c.Members.Any(m => m.UserId == me),
                Members = c.Members.Count,
            })
            .SingleOrDefaultAsync(ct);
        if (club is null) return NotFound();

        var problem = ClubRules.CannotJoin(club.AlreadyIn, await db.ClubMembers.CountAsync(m => m.UserId == me, ct), club.Members,
            await db.Blocks.AnyAsync(b => b.BlockerId == club.OwnerId && b.BlockedId == me, ct));
        if (problem is not null) return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        var status = ClubRules.StatusOnJoin(club.JoinPolicy);
        db.ClubMembers.Add(new ClubMember { ClubId = id, UserId = me, Role = ClubRoles.Member, Status = status, JoinedAt = clock.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Joined twice at the same moment: they're in.
        }

        if (status == ClubMemberStatus.Pending)
        {
            foreach (var manager in await Managers(id).ToListAsync(ct))
                await activity.NotifyAsync(manager, me, ActivityTypes.ClubRequest, clubId: id, ct: ct);
        }
        return (await LoadAsync(id, me, false, ct))!;
    }

    /// <summary>Leaves the club, or withdraws a request to join. The owner has to hand it over first.</summary>
    [HttpDelete(ApiRoutes.ClubMembership)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var membership = await db.ClubMembers.SingleOrDefaultAsync(m => m.ClubId == id && m.UserId == me, ct);
        if (membership is null) return NotFound();
        if (ClubRules.CannotLeave(membership.Role) is { } problem) return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        db.ClubMembers.Remove(membership);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Active members, owner and admins first. With <paramref name="pending"/>, the requests waiting (club admins only).</summary>
    [HttpGet(ApiRoutes.ClubMembers)]
    public async Task<ActionResult<IReadOnlyList<ClubMemberDto>>> Members(Guid id, bool pending = false, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!await Visible(me, await admins.IsAdminAsync(me, ct)).AnyAsync(c => c.Id == id, ct)) return NotFound();
        if (pending && !ClubRules.CanManage(await RoleAsync(id, me, ct))) return Forbid();

        var status = pending ? ClubMemberStatus.Pending : ClubMemberStatus.Active;
        var rows = await db.ClubMembers.AsNoTracking()
            .Where(m => m.ClubId == id && m.Status == status && m.User.SuspendedAt == null)
            .Where(m => !m.User.BlockedBy.Any(b => b.BlockerId == me))
            .Select(m => new ClubMemberDto(new UserSummaryDto(m.User.Id, m.User.DisplayName, m.User.AvatarUrl), m.Role, m.Status, m.JoinedAt,
                m.UserId == me))
            .ToListAsync(ct);
        return pending
            ? rows.OrderBy(m => m.JoinedAt).ToList()
            : rows.OrderBy(m => m.Role switch { ClubRoles.Owner => 0, ClubRoles.Admin => 1, _ => 2 })
                .ThenBy(m => m.User.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Club admins let someone in who asked to join; they're told.</summary>
    [HttpPost(ApiRoutes.ClubMemberApproval)]
    public async Task<IActionResult> Approve(Guid id, Guid userId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!ClubRules.CanManage(await RoleAsync(id, me, ct))) return Forbid();
        var request = await db.ClubMembers.SingleOrDefaultAsync(m => m.ClubId == id && m.UserId == userId, ct);
        if (request is null) return NotFound();
        if (request.Status == ClubMemberStatus.Active) return NoContent();
        if (await db.ClubMembers.CountAsync(m => m.ClubId == id && m.Status == ClubMemberStatus.Active, ct) >= ClubLimits.MaxMembers)
            return Problem("This club is full.", statusCode: StatusCodes.Status400BadRequest);

        request.Status = ClubMemberStatus.Active;
        request.JoinedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await activity.NotifyAsync(userId, me, ActivityTypes.ClubAccepted, clubId: id, ct: ct);
        return NoContent();
    }

    /// <summary>Removes a member, or turns down a request to join.</summary>
    [HttpDelete(ApiRoutes.ClubMember)]
    public async Task<IActionResult> Remove(Guid id, Guid userId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var actorRole = await RoleAsync(id, me, ct);
        if (actorRole is null) return NotFound();
        var target = await db.ClubMembers.SingleOrDefaultAsync(m => m.ClubId == id && m.UserId == userId, ct);
        if (target is null) return NotFound();
        if (ClubRules.CannotRemove(actorRole, target.Role, userId == me) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        db.ClubMembers.Remove(target);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} removed {MemberId} from club {ClubId}", me, userId, id);
        return NoContent();
    }

    /// <summary>
    /// The owner makes a member an admin, or a member again, or hands the club over: the new owner takes over and the
    /// old owner stays on as an admin.
    /// </summary>
    [HttpPut(ApiRoutes.ClubMemberRole)]
    public async Task<IActionResult> SetRole(Guid id, Guid userId, SetClubRoleRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var actor = await db.ClubMembers.SingleOrDefaultAsync(m => m.ClubId == id && m.UserId == me && m.Status == ClubMemberStatus.Active, ct);
        if (actor is null) return NotFound();
        var target = await db.ClubMembers.SingleOrDefaultAsync(m => m.ClubId == id && m.UserId == userId, ct);
        if (target is null) return NotFound();
        if (ClubRules.CannotSetRole(actor.Role, target.Status, request.Role, userId == me) is { } problem)
            return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        if (request.Role == ClubRoles.Owner)
        {
            // One owner at a time (a unique index): step down first, then hand over, in one transaction.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            actor.Role = ClubRoles.Admin;
            await db.SaveChangesAsync(ct);
            target.Role = ClubRoles.Owner;
            await db.Clubs.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.OwnerId, userId), ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            logger.LogInformation("User {UserId} handed club {ClubId} to {NewOwnerId}", me, id, userId);
        }
        else
        {
            target.Role = request.Role;
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    [HttpPost(ApiRoutes.ClubReports)]
    public async Task<IActionResult> Report(Guid id, CreateReportRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var ownerId = await Visible(me, reviewer: false).Where(c => c.Id == id).Select(c => (Guid?)c.OwnerId).SingleOrDefaultAsync(ct);
        if (ownerId is null) return NotFound();
        if (ownerId == me) return Problem("You can't report your own club.", statusCode: StatusCodes.Status400BadRequest);
        return this.ToResponse(await reports.SaveAsync(ReportTarget.Club(id), me, request, ct));
    }

    /// <summary>Site admins: clubs waiting for approval, longest-waiting first.</summary>
    [HttpGet(ApiRoutes.AdminPendingClubs)]
    public async Task<ActionResult<PagedResult<ClubSummaryDto>>> Pending(int page = 1, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } me || !await admins.IsAdminAsync(me, ct)) return Forbid();
        page = Math.Max(1, page);
        var query = db.Clubs.AsNoTracking().Where(c => c.ReviewStatus == ClubReviewStatus.Pending && c.Owner.SuspendedAt == null);
        var total = await query.CountAsync(ct);
        var items = (await SummariesAsync(query.OrderBy(c => c.CreatedAt).Skip((page - 1) * PageSize).Take(PageSize), me, ct))
            .Select(s => s.Club).ToList();
        return new PagedResult<ClubSummaryDto>(items, page, PageSize, total);
    }

    [HttpPost(ApiRoutes.AdminClubApproval)]
    public async Task<IActionResult> ApproveClub(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me || !await admins.IsAdminAsync(me, ct)) return Forbid();
        var club = await db.Clubs.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (club is null) return NotFound();
        if (club.ReviewStatus != ClubReviewStatus.Pending)
            return Problem("This club isn't waiting for approval any more.", statusCode: StatusCodes.Status409Conflict);

        (club.ReviewStatus, club.ReviewedAt, club.ReviewNote) = (ClubReviewStatus.Approved, clock.GetUtcNow(), null);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} approved club {ClubId}", me, id);
        await activity.NotifyAsync(club.OwnerId, me, ActivityTypes.ClubApproved, clubId: id, ct: ct);
        return NoContent();
    }

    /// <summary>Site admins: turns down a pending club, or takes down an approved one. It stays visible to its owner only.</summary>
    [HttpPost(ApiRoutes.AdminClubRejection)]
    public async Task<IActionResult> RejectClub(Guid id, RejectClubRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me || !await admins.IsAdminAsync(me, ct)) return Forbid();
        var club = await db.Clubs.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (club is null) return NotFound();
        if (club.ReviewStatus == ClubReviewStatus.Rejected) return NoContent();

        club.ReviewStatus = ClubReviewStatus.Rejected;
        club.ReviewedAt = clock.GetUtcNow();
        club.ReviewNote = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} rejected club {ClubId}", me, id);
        await activity.NotifyAsync(club.OwnerId, me, ActivityTypes.ClubRejected, clubId: id, snippet: club.ReviewNote, ct: ct);
        return NoContent();
    }

    private IQueryable<Club> Visible(Guid me, bool reviewer) => db.Clubs.AsNoTracking().Where(Moderation.ClubVisibleTo(me, reviewer));

    /// <summary>The caller's role in the club, if they're an active member.</summary>
    private Task<string?> RoleAsync(Guid clubId, Guid me, CancellationToken ct) =>
        db.ClubMembers.Where(m => m.ClubId == clubId && m.UserId == me && m.Status == ClubMemberStatus.Active)
            .Select(m => m.Role).SingleOrDefaultAsync(ct);

    /// <summary>Who hears about requests to join: the owner and admins, while their accounts are active.</summary>
    private IQueryable<Guid> Managers(Guid clubId) =>
        db.ClubMembers.Where(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Active
                                  && (m.Role == ClubRoles.Owner || m.Role == ClubRoles.Admin) && m.User.SuspendedAt == null)
            .Select(m => m.UserId);

    private async Task<ClubDto?> LoadAsync(Guid id, Guid me, bool isAdmin, CancellationToken ct)
    {
        var row = await Visible(me, isAdmin).Where(c => c.Id == id)
            .Select(c => new
            {
                c.Description, c.Latitude, c.Longitude, c.CreatedAt, c.OwnerId, c.ReviewNote,
                Owner = new UserSummaryDto(c.Owner.Id, c.Owner.DisplayName, c.Owner.AvatarUrl),
                Upheld = c.Reports.Any(r => r.Status == ReportStatus.Upheld),
                OpenReports = c.Reports.Count(r => r.Status == ReportStatus.Open),
                ReportedByMe = c.Reports.Any(r => r.ReporterId == me),
                Pending = c.Members.Count(m => m.Status == ClubMemberStatus.Pending && m.User.SuspendedAt == null),
            })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;

        var summary = (await SummariesAsync(db.Clubs.Where(c => c.Id == id), me, ct)).Single().Club;
        var manages = summary.MyStatus == ClubMemberStatus.Active && ClubRules.CanManage(summary.MyRole);
        var isOwner = row.OwnerId == me;
        return new ClubDto(summary, row.Description, row.Latitude, row.Longitude, row.Owner, row.CreatedAt,
            CanEdit: manages,
            CanManageMembers: manages,
            IsOwner: isOwner,
            CanJoin: summary.MyRole is null && summary.ReviewStatus == ClubReviewStatus.Approved,
            CanLeave: summary.MyRole is not null && !isOwner,
            CanReport: !isOwner && !row.ReportedByMe,
            CanReview: isAdmin && summary.ReviewStatus != ClubReviewStatus.Rejected && !isOwner,
            PendingRequests: manages ? row.Pending : 0,
            Moderation: Moderation.State(row.Upheld, row.OpenReports),
            ReviewNote: isOwner ? row.ReviewNote : null);
    }

    private sealed record SummaryRow(ClubSummaryDto Club, double? Latitude, double? Longitude);

    private static async Task<List<SummaryRow>> SummariesAsync(IQueryable<Club> query, Guid me, CancellationToken ct)
    {
        var rows = await query
            .Select(c => new
            {
                c.Id, c.Name, c.City, c.JoinPolicy, c.ReviewStatus, c.Latitude, c.Longitude,
                Count = c.Members.Count(m => m.Status == ClubMemberStatus.Active && m.User.SuspendedAt == null),
                Mine = c.Members.Where(m => m.UserId == me).Select(m => new { m.Role, m.Status }).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return rows.Select(r => new SummaryRow(
            new ClubSummaryDto(r.Id, r.Name, r.City, r.JoinPolicy, r.Count, r.ReviewStatus, r.Mine?.Role, r.Mine?.Status),
            r.Latitude, r.Longitude)).ToList();
    }

    private bool Validate(SaveClubRequest request)
    {
        foreach (var problem in ClubRules.CannotSave(request))
            ModelState.AddModelError(problem.Field ?? "", problem.Message);
        return ModelState.IsValid;
    }

    private static void Apply(Club club, SaveClubRequest request, DateTimeOffset now)
    {
        club.Name = request.Name.Trim();
        club.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        club.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        club.JoinPolicy = request.JoinPolicy;
        club.Latitude = ClubRules.Area(request.Latitude);
        club.Longitude = ClubRules.Area(request.Longitude);
        club.UpdatedAt = now;
    }
}
