using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhotoWorld.Api.Data;

namespace PhotoWorld.Api.Services.Notifications;

/// <summary>
/// The weekly summary email: once every 7 days per member who switched it on (confirmed address, not suspended),
/// counting what happened on their photos. A quiet week sends nothing but still moves on. A failed email is retried
/// on the next run. A Postgres advisory lock lets only one API instance send at a time.
/// </summary>
public sealed class WeeklySummarySender(
    IServiceScopeFactory scopes,
    IOptions<EmailOptions> options,
    TimeProvider clock,
    ILogger<WeeklySummarySender> logger) : BackgroundService
{
    // Arbitrary, fixed key for pg_try_advisory_lock ("PWWEEKLY").
    private const long LockKey = 0x50575745454B4C59;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.PollMinutes)), clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Weekly summary run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <returns>How many emails were sent.</returns>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoWorldDbContext>();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await AdvisoryLockAsync(db, "pg_try_advisory_lock", ct)) return 0;
            try
            {
                return await SendDueAsync(db, scope.ServiceProvider, ct);
            }
            finally
            {
                await AdvisoryLockAsync(db, "pg_advisory_unlock", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<int> SendDueAsync(PhotoWorldDbContext db, IServiceProvider services, CancellationToken ct)
    {
        var sender = services.GetRequiredService<IEmailSender>();
        var emails = services.GetRequiredService<NotificationEmails>();
        var now = clock.GetUtcNow();
        var dueBefore = now - NotificationRules.WeeklySummaryPeriod;
        var due = await db.EmailSubscriptions
            .Where(s => s.WeeklySummary && s.ConfirmedAt != null && s.User.SuspendedAt == null && s.WeeklySummaryFrom <= dueBefore)
            .Include(s => s.User)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var sub in due)
        {
            var stats = await StatsAsync(db, sub.UserId, sub.WeeklySummaryFrom, now, ct);
            if (!stats.IsQuiet)
            {
                try
                {
                    await sender.SendAsync(emails.WeeklySummary(sub.Email, sub.User.DisplayName, stats, sub.UnsubscribeToken), ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Could not send member {UserId} their weekly summary; will retry", sub.UserId);
                    continue;
                }
                sent++;
            }
            sub.WeeklySummaryFrom = now;
            await db.SaveChangesAsync(ct);
        }

        if (sent > 0) logger.LogInformation("Sent {Count} weekly summary emails", sent);
        return sent;
    }

    /// <summary>The week's numbers. Other members only (never the member's own likes or comments).</summary>
    internal static async Task<WeeklyStats> StatsAsync(PhotoWorldDbContext db, Guid me, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var mine = db.Photos.Where(p => p.OwnerId == me).Select(p => p.Id);
        var (firstDay, lastDay) = (DateOnly.FromDateTime(from.UtcDateTime), DateOnly.FromDateTime(to.UtcDateTime));
        var views = await db.PhotoViewDays.Where(v => mine.Contains(v.PhotoId) && v.Day >= firstDay && v.Day < lastDay)
            .SumAsync(v => (int?)v.Views, ct) ?? 0;
        var likes = await db.PhotoLikes.CountAsync(l => mine.Contains(l.PhotoId) && l.UserId != me && l.CreatedAt > from && l.CreatedAt <= to, ct);
        var comments = await db.PhotoComments.CountAsync(c => mine.Contains(c.PhotoId) && c.AuthorId != me && c.CreatedAt > from && c.CreatedAt <= to, ct);
        var feedback = await db.PhotoFeedback.CountAsync(f => mine.Contains(f.PhotoId) && f.CreatedAt > from && f.CreatedAt <= to, ct);
        var followers = await db.Follows.CountAsync(f => f.FolloweeId == me && f.CreatedAt > from && f.CreatedAt <= to, ct);
        var weekAhead = to + NotificationRules.WeeklySummaryPeriod;
        var walks = await db.Walks.CountAsync(w => w.CancelledAt == null && w.StartsAt > to && w.StartsAt <= weekAhead
            && w.Organizer.SuspendedAt == null
            // Public walks by people they follow, and walks of clubs they belong to.
            && (w.ClubId == null
                ? db.Follows.Any(f => f.FollowerId == me && f.FolloweeId == w.OrganizerId)
                : db.ClubMembers.Any(m => m.ClubId == w.ClubId && m.UserId == me && m.Status == Shared.Clubs.ClubMemberStatus.Active)), ct);
        var challenges = await db.Challenges.CountAsync(c => c.StartsAt <= to && c.EndsAt > to
            && (c.ClubId == null
                || db.ClubMembers.Any(m => m.ClubId == c.ClubId && m.UserId == me && m.Status == Shared.Clubs.ClubMemberStatus.Active)), ct);
        return new WeeklyStats(views, likes, comments, feedback, followers, walks, challenges);
    }

    private static async Task<bool> AdvisoryLockAsync(PhotoWorldDbContext db, string function, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"select {function}({LockKey})";
        return await cmd.ExecuteScalarAsync(ct) is true;
    }
}
