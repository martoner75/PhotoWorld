using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services.Challenges;
using PhotoWorld.Shared.Activities;

namespace PhotoWorld.Api.Services.Activities;

/// <summary>
/// Every few minutes: tells members about challenges (a new one opening, one they entered closing within a day, and
/// the results) and photo owners about their photo of the day, and deletes activity older than <see cref="ActivityRules.KeepFor"/>. Each piece of news is claimed
/// with a conditional update first, so it goes out once even with several API instances.
/// </summary>
public sealed class ScheduledActivitySender(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ScheduledActivitySender> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Challenge activity run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoWorldDbContext>();
        var now = clock.GetUtcNow();

        var recent = now - ActivityRules.ChallengeNewsWindow;
        var challenges = await db.Challenges.AsNoTracking()
            .Where(c => c.StartsAt <= now && c.EndsAt > recent
                        && (c.OpenNotifiedAt == null || c.EndingNotifiedAt == null || c.ResultsNotifiedAt == null))
            .ToListAsync(ct);

        foreach (var c in challenges)
        {
            var due = ActivityRules.ChallengeNewsDue(c.StartsAt, c.EndsAt,
                c.OpenNotifiedAt is not null, c.EndingNotifiedAt is not null, c.ResultsNotifiedAt is not null, now);

            if (due.Open && await ClaimAsync(db, c.Id, x => x.OpenNotifiedAt, now, ct))
            {
                // Everyone active who wants challenge news.
                var everyone = await db.Users.Where(u => u.SuspendedAt == null)
                    .Where(u => !db.ActivitySettings.Any(s => s.UserId == u.Id && !s.Challenges))
                    .Select(u => u.Id).ToListAsync(ct);
                await AddAsync(db, everyone.Select(id => News(id, c.Id, ActivityTypes.ChallengeOpen, null, now)), ct);
            }

            if (due.Ending && await ClaimAsync(db, c.Id, x => x.EndingNotifiedAt, now, ct))
            {
                var entrants = await Entrants(db, c.Id).ToListAsync(ct);
                await AddAsync(db, entrants.Select(id => News(id, c.Id, ActivityTypes.ChallengeEnding, null, now)), ct);
            }

            if (due.Results && await ClaimAsync(db, c.Id, x => x.ResultsNotifiedAt, now, ct))
            {
                var standing = (await new ChallengeStandings(db).ForAsync([c.Id], ct)).GetValueOrDefault(c.Id, Standing.Empty);
                // Each entrant's best place among their entries (top three only); everyone who entered hears the results.
                var places = standing.Ranked.Take(3).Select((s, i) => (s.OwnerId, Place: i + 1))
                    .GroupBy(p => p.OwnerId).ToDictionary(g => g.Key, g => g.Min(p => p.Place));
                var entrants = await Entrants(db, c.Id).ToListAsync(ct);
                await AddAsync(db, entrants.Select(id => News(id, c.Id, ActivityTypes.ChallengeResult,
                    places.TryGetValue(id, out var place) ? place : null, now)), ct);
            }
        }

        // Today's photo of the day: tell its owner once (claimed like the challenge news).
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var pick = await db.PhotosOfTheDay.AsNoTracking().Where(x => x.Date == today && x.NotifiedAt == null)
            .Select(x => new { x.PhotoId, x.Photo.OwnerId }).SingleOrDefaultAsync(ct);
        if (pick is not null && await db.PhotosOfTheDay.Where(x => x.Date == today && x.NotifiedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NotifiedAt, now), ct) == 1)
            await AddAsync(db, [new Activity { RecipientId = pick.OwnerId, PhotoId = pick.PhotoId, Type = ActivityTypes.PhotoOfTheDay, CreatedAt = now }], ct);

        // Photo walks starting within a day: remind attendees and the organiser once.
        var soon = now.AddDays(1);
        var walks = await db.Walks.AsNoTracking()
            .Where(w => w.CancelledAt == null && w.ReminderSentAt == null && w.StartsAt > now && w.StartsAt <= soon)
            .Select(w => new { w.Id, w.OrganizerId, Attendees = w.Attendees.Select(a => a.UserId).ToList() })
            .ToListAsync(ct);
        foreach (var w in walks)
        {
            if (await db.Walks.Where(x => x.Id == w.Id && x.ReminderSentAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReminderSentAt, now), ct) != 1) continue;
            var feed = scope.ServiceProvider.GetRequiredService<ActivityFeed>();
            await feed.NotifyWalkAsync([w.OrganizerId, .. w.Attendees], null, w.OrganizerId, w.Id, ActivityTypes.WalkReminder, ct);
        }

        var purged = await db.Activities.Where(a => a.CreatedAt < now - ActivityRules.KeepFor).ExecuteDeleteAsync(ct);
        if (purged > 0) logger.LogInformation("Deleted {Count} old activity items", purged);
    }

    /// <summary>Marks a piece of news as sent; true only for the instance whose update made the change.</summary>
    private static async Task<bool> ClaimAsync(PhotoWorldDbContext db, Guid id,
        System.Linq.Expressions.Expression<Func<Challenge, DateTimeOffset?>> marker, DateTimeOffset now, CancellationToken ct)
    {
        var isUnsent = System.Linq.Expressions.Expression.Lambda<Func<Challenge, bool>>(
            System.Linq.Expressions.Expression.Equal(marker.Body, System.Linq.Expressions.Expression.Constant(null, typeof(DateTimeOffset?))),
            marker.Parameters);
        return await db.Challenges.Where(c => c.Id == id).Where(isUnsent)
            .ExecuteUpdateAsync(s => s.SetProperty(marker, now), ct) == 1;
    }

    /// <summary>Members who entered the challenge and still want challenge news.</summary>
    private static IQueryable<Guid> Entrants(PhotoWorldDbContext db, Guid challengeId) =>
        db.ChallengeEntries.Where(e => e.ChallengeId == challengeId)
            .Select(e => e.Photo.Owner)
            .Where(u => u.SuspendedAt == null && !db.ActivitySettings.Any(s => s.UserId == u.Id && !s.Challenges))
            .Select(u => u.Id).Distinct();

    private static Activity News(Guid recipientId, Guid challengeId, string type, int? place, DateTimeOffset now) => new()
    {
        RecipientId = recipientId, ChallengeId = challengeId, Type = type, Place = place, CreatedAt = now,
    };

    private async Task AddAsync(PhotoWorldDbContext db, IEnumerable<Activity> items, CancellationToken ct)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        db.Activities.AddRange(list);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Sent {Count} {Type} activity items", list.Count, list[0].Type);
    }
}
