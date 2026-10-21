using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Notifications;

namespace PhotoWorld.Api.Services.Notifications;

/// <summary>
/// Emails subscribers about newly published blog posts: instantly (every run) or as daily/weekly digests.
/// Each subscription keeps a cursor (<see cref="EmailSubscription.NotifiedThrough"/>), so a post is sent once; a failed
/// email leaves the cursor alone and is retried on the next run. A Postgres advisory lock lets only one API
/// instance send at a time.
/// </summary>
public sealed class BlogNotificationSender(
    IServiceScopeFactory scopes,
    IOptions<EmailOptions> options,
    TimeProvider clock,
    ILogger<BlogNotificationSender> logger) : BackgroundService
{
    // Arbitrary, fixed key for pg_try_advisory_lock ("PWNOTIFY").
    private const long LockKey = 0x50574E4F54494659;

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
                logger.LogError(ex, "Blog notification run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <returns>How many emails were sent.</returns>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoWorldDbContext>();

        // Keep one connection for the whole run: the advisory lock belongs to the database session.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await AdvisoryLockAsync(db, "pg_try_advisory_lock", ct))
            {
                logger.LogDebug("Another instance is sending blog notifications");
                return 0;
            }
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
        var storage = services.GetRequiredService<SupabaseStorage>();

        var now = clock.GetUtcNow();
        var cutoff = now - NotificationRules.PublishSettleTime;
        // Suspended members get nothing until they're reactivated.
        var subscriptions = await db.EmailSubscriptions
            .Where(s => s.Enabled && s.ConfirmedAt != null && s.User.SuspendedAt == null).ToListAsync(ct);

        var sent = 0;
        foreach (var sub in subscriptions)
        {
            if (!NotificationRules.IsDue(sub.Frequency, sub.PeriodStartedAt, now)) continue;

            // What this subscriber could see on the site: published, not hidden by moderation, not their own.
            var news = db.BlogPosts.Where(Data.Moderation.PostVisibleTo(sub.UserId))
                .Where(p => p.Status == BlogPostStatus.Published && p.AuthorId != sub.UserId
                            && p.PublishedAt > sub.NotifiedThrough && p.PublishedAt <= cutoff);
            var total = await news.CountAsync(ct);
            if (total > 0)
            {
                var posts = (await news.OrderBy(p => p.PublishedAt)
                        .Take(NotificationRules.MaxPostsPerEmail)
                        .Select(p => new
                        {
                            p.Id, p.Title, p.Excerpt, Author = p.Author.DisplayName,
                            Cover = p.CoverPhoto == null ? null : p.CoverPhoto.ThumbnailPath,
                        })
                        .ToListAsync(ct))
                    .Select(p => new PostNotice(p.Id, p.Title, p.Excerpt, p.Author, p.Cover is null ? null : storage.PublicUrl(p.Cover)))
                    .ToList();
                try
                {
                    await sender.SendAsync(emails.NewPosts(sub.Email, sub.Frequency, posts, total - posts.Count, sub.UnsubscribeToken), ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Could not email member {UserId} about {Count} new posts; will retry", sub.UserId, total);
                    continue;
                }
                sub.LastSentAt = now;
                sent++;
            }

            if (sub.NotifiedThrough < cutoff) sub.NotifiedThrough = cutoff;
            if (sub.Frequency != NotificationFrequency.Instant) sub.PeriodStartedAt = now;
            await db.SaveChangesAsync(ct);
        }

        if (sent > 0) logger.LogInformation("Sent {Count} blog notification emails", sent);
        return sent;
    }

    private static async Task<bool> AdvisoryLockAsync(PhotoWorldDbContext db, string function, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"select {function}({LockKey})";
        return await cmd.ExecuteScalarAsync(ct) is true;
    }
}
