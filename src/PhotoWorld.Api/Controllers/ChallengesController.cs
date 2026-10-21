using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Challenges;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Controllers;

/// <summary>Themed photo challenges: members enter their own photos, and likes during the challenge are the votes.</summary>
[ApiController]
[Authorize]
public class ChallengesController(
    PhotoWorldDbContext db,
    ChallengeStandings standings,
    SupabaseStorage storage,
    TimeProvider clock,
    ILogger<ChallengesController> logger) : ControllerBase
{
    /// <summary>Most past challenges listed; older ones drop off the page.</summary>
    private const int MaxListed = 60;

    /// <summary>Open and upcoming challenges, then past ones, each group ordered by end date.</summary>
    [HttpGet(ApiRoutes.Challenges)]
    public async Task<IReadOnlyList<ChallengeSummaryDto>> List(CancellationToken ct)
    {
        var challenges = await db.Challenges.AsNoTracking()
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
        var challenge = await db.Challenges.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (challenge is null) return NotFound();
        var me = User.GetUserId();

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
        return new ChallengeDto(summary, entries, mine);
    }

    [HttpPost(ApiRoutes.ChallengeEntries)]
    public async Task<IActionResult> Enter(Guid id, EnterChallengeRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var challenge = await db.Challenges.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (challenge is null) return NotFound();

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
                photo is null ? null : storage.PublicUrl(photo.ThumbnailPath),
                phase == ChallengePhase.Closed ? photo?.Owner : null);
        }).ToList();
    }
}
