using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhotoWorld.Api.Data;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Services.Push;

/// <summary>
/// Sends new activity (from the last hour) to the recipients' phones, as the same text the Activity screen shows, with
/// the unread count as the app badge. Each item is handled once (PushedAt); members with push switched off, or without
/// a registered phone, just get it marked. Dead tokens are removed. A Postgres advisory lock lets one API instance send.
/// Doesn't run at all until APNs or FCM keys are configured.
/// </summary>
public sealed class PushDispatcher(
    IServiceScopeFactory scopes,
    IEnumerable<IPushChannel> channels,
    IOptions<PushOptions> options,
    TimeProvider clock,
    ILogger<PushDispatcher> logger) : BackgroundService
{
    // Arbitrary, fixed key for pg_try_advisory_lock ("PWPUSH").
    private const long LockKey = 0x5057505553480000;
    private const int Batch = 200;

    private readonly Dictionary<string, IPushChannel> _channels =
        channels.Where(c => c.IsConfigured).ToDictionary(c => c.Platform);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channels.Count == 0)
        {
            logger.LogInformation("Push notifications are off: no APNs or FCM keys configured (see README → Push notifications)");
            return;
        }
        logger.LogInformation("Push notifications on for: {Platforms}", string.Join(", ", _channels.Keys));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, options.Value.PollSeconds)), clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Push run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoWorldDbContext>();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await AdvisoryLockAsync(db, "pg_try_advisory_lock", ct)) return 0;
            try { return await SendDueAsync(db, ct); }
            finally { await AdvisoryLockAsync(db, "pg_advisory_unlock", CancellationToken.None); }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<int> SendDueAsync(PhotoWorldDbContext db, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var since = now.AddHours(-1);
        var due = await db.Activities.AsNoTracking()
            .Where(a => a.PushedAt == null && a.CreatedAt >= since)
            .OrderBy(a => a.CreatedAt).Take(Batch)
            .Select(a => new
            {
                a.Id, a.RecipientId, a.Type, a.PhotoId, a.PostId, a.ChallengeId, a.Place, a.Snippet, a.CreatedAt,
                Actor = a.Actor == null ? null : new UserSummaryDto(a.Actor.Id, a.Actor.DisplayName, a.Actor.AvatarUrl),
                PhotoTitle = a.Photo == null ? null : a.Photo.Title,
                PostTitle = a.Post == null ? null : a.Post.Title,
                ChallengeTitle = a.Challenge == null ? null : a.Challenge.Title,
                a.WalkId, WalkTitle = a.Walk == null ? null : a.Walk.Title,
                a.ClubId, ClubName = a.Club == null ? null : a.Club.Name,
                a.WorkshopId, WorkshopTitle = a.Workshop == null ? null : a.Workshop.Title,
                Wanted = a.Recipient.SuspendedAt == null && !db.ActivitySettings.Any(s => s.UserId == a.RecipientId && !s.Push),
            })
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        var ids = due.Select(a => a.Id).ToList();
        await db.Activities.Where(a => ids.Contains(a.Id)).ExecuteUpdateAsync(s => s.SetProperty(a => a.PushedAt, now), ct);

        var recipients = due.Where(a => a.Wanted).Select(a => a.RecipientId).Distinct().ToList();
        var devices = (await db.Devices.AsNoTracking().Where(d => recipients.Contains(d.UserId)).ToListAsync(ct))
            .Where(d => _channels.ContainsKey(d.Platform)).ToLookup(d => d.UserId);
        var unread = await db.Activities.Where(a => recipients.Contains(a.RecipientId) && a.ReadAt == null)
            .GroupBy(a => a.RecipientId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        var sent = 0;
        foreach (var a in due.Where(a => a.Wanted && devices[a.RecipientId].Any()))
        {
            var dto = new ActivityDto(a.Id, a.Type, a.Actor, a.PhotoId, a.PhotoTitle, null, a.PostId, a.PostTitle, a.ChallengeId,
                a.ChallengeTitle, a.Place, a.Snippet, a.CreatedAt, false, a.WalkId, a.WalkTitle, a.ClubId, a.ClubName,
                a.WorkshopId, a.WorkshopTitle);
            var message = PushText.For(dto, unread.GetValueOrDefault(a.RecipientId));
            foreach (var device in devices[a.RecipientId])
            {
                var result = await _channels[device.Platform].SendAsync(device.Token, message, ct);
                if (result == PushResult.Sent) sent++;
                else if (result == PushResult.InvalidToken)
                    await db.Devices.Where(d => d.Id == device.Id).ExecuteDeleteAsync(ct);
            }
        }
        if (sent > 0) logger.LogInformation("Sent {Count} push notifications", sent);
        return sent;
    }

    private static async Task<bool> AdvisoryLockAsync(PhotoWorldDbContext db, string function, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"select {function}({LockKey})";
        return await cmd.ExecuteScalarAsync(ct) is true;
    }
}

/// <summary>What a push notification says and opens, from the same wording as the Activity screen.</summary>
public static class PushText
{
    public static PushMessage For(ActivityDto activity, int unread)
    {
        var data = new Dictionary<string, string> { ["activityId"] = activity.Id.ToString(), ["type"] = activity.Type };
        if (activity.PhotoId is { } photo) data["photoId"] = photo.ToString();
        if (activity.PostId is { } post) data["postId"] = post.ToString();
        if (activity.ChallengeId is { } challenge) data["challengeId"] = challenge.ToString();
        if (activity.WalkId is { } walk) data["walkId"] = walk.ToString();
        if (activity.ClubId is { } club) data["clubId"] = club.ToString();
        if (activity.WorkshopId is { } workshop) data["workshopId"] = workshop.ToString();
        if (activity.Actor is { } actor) data["memberId"] = actor.Id.ToString();
        var body = ActivityText.Describe(activity) + (activity.Snippet is { } s ? $": “{s}”" : "");
        return new PushMessage("PhotoCommunity", body, unread > 0 ? unread : null, data);
    }
}
