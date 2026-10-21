using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;

namespace PhotoWorld.Api.Services.Challenges;

/// <param name="Ranked">Entries anyone may see, best first (see <see cref="ChallengeRules.Rank"/>).</param>
/// <param name="Hidden">Entries hidden by moderation: they show only to their owner and can't place.</param>
public sealed record Standing(IReadOnlyList<EntryScore> Ranked, IReadOnlyList<EntryScore> Hidden)
{
    public static readonly Standing Empty = new([], []);
}

/// <summary>
/// Scores challenge entries. A vote is a like from another member between the moment the photo was entered and the
/// challenge's end, so likes from before the challenge or after it closed don't count, and neither does the owner's own.
/// Standings are worked out when asked for rather than stored, so a winner stays the winner unless their photo is
/// deleted or removed by moderation.
/// </summary>
public sealed class ChallengeStandings(PhotoWorldDbContext db)
{
    public async Task<Dictionary<Guid, Standing>> ForAsync(IReadOnlyCollection<Guid> challengeIds, CancellationToken ct)
    {
        var publicPhotos = db.Photos.Where(Data.Moderation.PhotoVisibleTo(null));
        var rows = await db.ChallengeEntries.AsNoTracking()
            .Where(e => challengeIds.Contains(e.ChallengeId))
            .Select(e => new
            {
                e.ChallengeId, e.PhotoId, e.Photo.OwnerId, e.CreatedAt,
                Votes = e.Photo.Likes.Count(l => l.UserId != e.Photo.OwnerId
                                                 && l.CreatedAt >= e.CreatedAt && l.CreatedAt < e.Challenge.EndsAt),
                IsPublic = publicPhotos.Any(p => p.Id == e.PhotoId),
            })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.ChallengeId).ToDictionary(g => g.Key, g => new Standing(
            ChallengeRules.Rank(g.Where(r => r.IsPublic).Select(r => new EntryScore(r.PhotoId, r.OwnerId, r.Votes, r.CreatedAt))),
            g.Where(r => !r.IsPublic).Select(r => new EntryScore(r.PhotoId, r.OwnerId, r.Votes, r.CreatedAt)).ToList()));
    }
}
