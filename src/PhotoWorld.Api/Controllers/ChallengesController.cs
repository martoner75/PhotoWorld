using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Challenges;
using PhotoWorld.Api.Services.Clubs;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>Themed photo challenges: members enter their own photos, and likes during the challenge are the votes.</summary>
[ApiController]
[Authorize]
public class ChallengesController(
    PhotoWorldDbContext db,
    ChallengeStandings standings,
    AdminAccess admins,
    SupabaseStorage storage,
    TimeProvider clock,
    ILogger<ChallengesController> logger) : ControllerBase
{
    /// <summary>Most past challenges listed; older ones drop off the page.</summary>
    private const int MaxListed = 60;

    /// <summary>
    /// Open and upcoming challenges, then past ones, each group ordered by end date. Site-wide ones, or with
    /// <paramref name="clubId"/> that club's (for anyone who can see the club).
    /// </summary>
    [HttpGet(ApiRoutes.Challenges)]
    public async Task<ActionResult<IReadOnlyList<ChallengeSummaryDto>>> List(Guid? clubId = null, CancellationToken ct = default)
    {
        if (clubId is { } club && !await ClubVisibleAsync(club, User.GetUserId(), ct)) return NotFound();
        var challenges = await db.Challenges.AsNoTracking().Include(c => c.Club)
            .Where(c => c.ClubId == clubId)
            .OrderByDescending(c => c.EndsAt).Take(MaxListed).ToListAsync(ct);
        var scores = await standings.ForAsync(challenges.Select(c => c.Id).ToList(), ct);
        var summaries = await SummariesAsync(challenges, scores, ct);

        return summaries
            .OrderBy(s => s.Phase == ChallengePhase.Closed)
            .ThenBy(s => s.Phase == ChallengePhase.Closed ? -s.EndsAt.UtcTicks : s.EndsAt.UtcTicks)
            .ToList();
    }

    [HttpGet(ApiRoutes.Challenge)]
    public async Task<ActionResult<ChallengeDto>> Get(Guid id, CancellationToken ct)
    {
        var challenge = await db.Challenges.AsNoTracking().Include(c => c.Club).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (challenge is null) return NotFound();
        var me = User.GetUserId();
        if (challenge.ClubId is { } clubId && !await ClubVisibleAsync(clubId, me, ct)) return NotFound();

        var standing = (await standings.ForAsync([id], ct)).GetValueOrDefault(id, Standing.Empty);
        // Hidden entries only show to their owner, after the ranked ones and without a place.
        var shown = standing.Ranked.Select((s, i) => (Score: s, Rank: i + 1))
            .Concat(standing.Hidden.Where(s => s.OwnerId == me).Select(s => (Score: s, Rank: 0)))
            .ToList();

        var photoIds = shown.Select(s => s.Score.PhotoId).ToList();
        // Entries by members the viewer blocked are left out (their places still count for everyone else).
        var photos = (await db.Photos.AsNoTracking().Where(Moderation.PhotoVisibleTo(me))
                .Where(p => photoIds.Contains(p.Id)).ToSummariesAsync(me, storage, ct))
            .ToDictionary(p => p.Id);
        var entries = shown.Where(s => photos.ContainsKey(s.Score.PhotoId))
            .Select(s => new ChallengeEntryDto(photos[s.Score.PhotoId], s.Score.Votes, s.Rank, s.Score.OwnerId == me, s.Score.EnteredAt))
            .ToList();

        var summary = (await SummariesAsync([challenge], new() { [id] = standing }, ct)).Single();
        var mine = standing.Ranked.Concat(standing.Hidden).Count(s => s.OwnerId == me);
        var canEnter = challenge.ClubId is not { } club || ClubRules.CannotEnterClubChallenge(await IsActiveMemberAsync(club, me, ct)) is null;
        return new ChallengeDto(summary, entries, mine, await CanManageAsync(challenge.ClubId, me, ct), canEnter);
    }

    // ----- Running challenges: site admins for site-wide ones, a club's owner and admins for the club's -----

    [HttpPost(ApiRoutes.Challenges)]
    public async Task<ActionResult<ChallengeSummaryDto>> Create(SaveChallengeRequest request, CancellationToken ct)
    {
        var me = User.GetUserId();
        if (request.ClubId is { } clubId)
        {
            var club = await db.Clubs.AsNoTracking().Where(Moderation.ClubVisibleTo(me)).Where(c => c.Id == clubId)
                .Select(c => new { Approved = c.ReviewStatus == ClubReviewStatus.Approved }).SingleOrDefaultAsync(ct);
            if (club is null) return NotFound();
            if (ClubRules.CannotRunChallenge(await ClubRoleAsync(clubId, me, ct), club.Approved) is { } problem)
                return Problem(problem, statusCode: StatusCodes.Status403Forbidden);
        }
        else if (!await admins.IsAdminAsync(me, ct))
        {
            return Forbid();
        }
        if (!IsValid(request, isNew: true)) return ValidationProblem(ModelState);

        var challenge = new Challenge
        {
            Title = request.Title.Trim(),
            Description = Clean(request.Description),
            StartsAt = request.StartsAt.ToUniversalTime(),
            EndsAt = request.EndsAt.ToUniversalTime(),
            CreatedAt = clock.GetUtcNow(),
            ClubId = request.ClubId,
        };
        db.Challenges.Add(challenge);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} created challenge {ChallengeId} \"{Title}\"", User.GetUserId(), challenge.Id, challenge.Title);
        return CreatedAtAction(nameof(Get), new { id = challenge.Id },
            (await SummariesAsync([challenge], [], ct)).Single());
    }

    /// <summary>Changes a challenge. Moving the dates of one that's running changes which likes count as votes.</summary>
    [HttpPut(ApiRoutes.Challenge)]
    public async Task<ActionResult<ChallengeSummaryDto>> Update(Guid id, SaveChallengeRequest request, CancellationToken ct)
    {
        var challenge = await db.Challenges.Include(c => c.Club).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (challenge is null) return NotFound();
        if (!await CanManageAsync(challenge.ClubId, User.GetUserId(), ct)) return Forbid();
        if (!IsValid(request, isNew: false)) return ValidationProblem(ModelState);

        challenge.Title = request.Title.Trim();
        challenge.Description = Clean(request.Description);
        challenge.StartsAt = request.StartsAt.ToUniversalTime();
        challenge.EndsAt = request.EndsAt.ToUniversalTime();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} changed challenge {ChallengeId}", User.GetUserId(), id);
        var scores = await standings.ForAsync([id], ct);
        return (await SummariesAsync([challenge], scores, ct)).Single();
    }

    /// <summary>Deletes a challenge and its entries; the photos themselves stay.</summary>
    [HttpDelete(ApiRoutes.Challenge)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var clubId = await db.Challenges.Where(c => c.Id == id).Select(c => new { c.ClubId }).SingleOrDefaultAsync(ct);
        if (clubId is null) return NotFound();
        if (!await CanManageAsync(clubId.ClubId, User.GetUserId(), ct)) return Forbid();
        await db.Challenges.Where(c => c.Id == id).ExecuteDeleteAsync(ct);

        logger.LogInformation("User {UserId} deleted challenge {ChallengeId}", User.GetUserId(), id);
        return NoContent();
    }

    private bool IsValid(SaveChallengeRequest request, bool isNew)
    {
        foreach (var (field, message) in ChallengeRules.CannotSave(request.Title, request.StartsAt, request.EndsAt, clock.GetUtcNow(), isNew))
            ModelState.AddModelError(field, message);
        return ModelState.IsValid;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private Task<bool> ClubVisibleAsync(Guid clubId, Guid? me, CancellationToken ct) =>
        db.Clubs.Where(Moderation.ClubVisibleTo(me)).AnyAsync(c => c.Id == clubId, ct);

    private async Task<string?> ClubRoleAsync(Guid clubId, Guid? me, CancellationToken ct) => me is null ? null
        : await db.ClubMembers.Where(m => m.ClubId == clubId && m.UserId == me && m.Status == ClubMemberStatus.Active)
            .Select(m => m.Role).SingleOrDefaultAsync(ct);

    private async Task<bool> IsActiveMemberAsync(Guid clubId, Guid? me, CancellationToken ct) => await ClubRoleAsync(clubId, me, ct) is not null;

    /// <summary>Site admins run every challenge; a club's owner and admins also run the club's.</summary>
    private async Task<bool> CanManageAsync(Guid? clubId, Guid? me, CancellationToken ct) =>
        await admins.IsAdminAsync(me, ct) || (clubId is { } club && ClubRules.CanManage(await ClubRoleAsync(club, me, ct)));

    [HttpPost(ApiRoutes.ChallengeEntries)]
    public async Task<IActionResult> Enter(Guid id, EnterChallengeRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var challenge = await db.Challenges.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (challenge is null) return NotFound();
        if (challenge.ClubId is { } clubId)
        {
            if (!await ClubVisibleAsync(clubId, me, ct)) return NotFound();
            if (ClubRules.CannotEnterClubChallenge(await IsActiveMemberAsync(clubId, me, ct)) is { } notMember)
                return Problem(notMember, statusCode: StatusCodes.Status400BadRequest);
        }

        var ownsVisiblePhoto = await db.Photos
            .Where(p => p.Id == request.PhotoId && p.OwnerId == me)
            .Where(Moderation.PhotoVisibleTo(null))
            .AnyAsync(ct);
        var alreadyEntered = await db.ChallengeEntries.AnyAsync(e => e.ChallengeId == id && e.PhotoId == request.PhotoId, ct);
        var myEntries = await db.ChallengeEntries.CountAsync(e => e.ChallengeId == id && e.Photo.OwnerId == me, ct);

        var now = clock.GetUtcNow();
        var problem = ChallengeRules.CannotEnter(ChallengeRules.Phase(challenge.StartsAt, challenge.EndsAt, now),
            ownsVisiblePhoto, alreadyEntered, myEntries);
        if (problem is not null) return Problem(problem, statusCode: StatusCodes.Status400BadRequest);

        db.ChallengeEntries.Add(new ChallengeEntry { ChallengeId = id, PhotoId = request.PhotoId, CreatedAt = now });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Entered twice at the same moment: the entry already exists.
            return Problem("That photo is already entered.", statusCode: StatusCodes.Status400BadRequest);
        }

        logger.LogInformation("User {UserId} entered photo {PhotoId} in challenge {ChallengeId}", me, request.PhotoId, id);
        return NoContent();
    }

    /// <summary>Withdraws one of the caller's photos while the challenge is still open.</summary>
    [HttpDelete(ApiRoutes.ChallengeEntry)]
    public async Task<IActionResult> Withdraw(Guid id, Guid photoId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var entry = await db.ChallengeEntries.Include(e => e.Challenge).Include(e => e.Photo)
            .SingleOrDefaultAsync(e => e.ChallengeId == id && e.PhotoId == photoId, ct);
        if (entry is null) return NotFound();
        if (entry.Photo.OwnerId != me) return Forbid();
        if (ChallengeRules.Phase(entry.Challenge.StartsAt, entry.Challenge.EndsAt, clock.GetUtcNow()) != ChallengePhase.Open)
            return Problem("This challenge has ended, so its entries are final.", statusCode: StatusCodes.Status400BadRequest);

        db.ChallengeEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} withdrew photo {PhotoId} from challenge {ChallengeId}", me, photoId, id);
        return NoContent();
    }

    private async Task<List<ChallengeSummaryDto>> SummariesAsync(IReadOnlyList<Challenge> challenges,
        Dictionary<Guid, Standing> scores, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var leaders = challenges
            .Select(c => (Challenge: c, Leader: scores.GetValueOrDefault(c.Id, Standing.Empty).Ranked.FirstOrDefault()))
            .ToList();

        var leaderPhotoIds = leaders.Where(l => l.Leader is not null).Select(l => l.Leader!.PhotoId).ToList();
        var leaderPhotos = await db.Photos.AsNoTracking()
            .Where(p => leaderPhotoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ThumbnailPath, Owner = new UserSummaryDto(p.Owner.Id, p.Owner.DisplayName, p.Owner.AvatarUrl) })
            .ToDictionaryAsync(p => p.Id, ct);

        return leaders.Select(l =>
        {
            var c = l.Challenge;
            var phase = ChallengeRules.Phase(c.StartsAt, c.EndsAt, now);
            var photo = l.Leader is null ? null : leaderPhotos.GetValueOrDefault(l.Leader.PhotoId);
            return new ChallengeSummaryDto(c.Id, c.Title, c.Description, c.StartsAt, c.EndsAt, phase,
                scores.GetValueOrDefault(c.Id, Standing.Empty).Ranked.Count,
                photo is null ? null : storage.ThumbnailUrl(photo.ThumbnailPath),
                phase == ChallengePhase.Closed ? photo?.Owner : null, c.ClubId, c.Club?.Name);
        }).ToList();
    }
}
