using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Shared.Activities;

namespace PhotoWorld.Api.Services.Activities;

/// <summary>
/// Records activity for members. Nobody is notified about their own actions, by someone they blocked, or about kinds
/// of activity they switched off. Notifying never fails the action that caused it: errors are logged and swallowed.
/// </summary>
public sealed class ActivityFeed(PhotoWorldDbContext db, TimeProvider clock, ILogger<ActivityFeed> logger)
{
    /// <summary>Tells one member that <paramref name="actorId"/> did something.</summary>
    public Task NotifyAsync(Guid recipientId, Guid actorId, string type, Guid? photoId = null, Guid? postId = null,
        string? snippet = null, CancellationToken ct = default) =>
        SafelyAsync(type, async () =>
        {
            if (recipientId == actorId) return;
            if (!ActivityRules.Wants(await SettingsAsync(recipientId, ct), type)) return;
            if (await db.Blocks.AnyAsync(b => b.BlockerId == recipientId && b.BlockedId == actorId, ct)) return;
            if (ActivityRules.IsOncePerActor(type) && await db.Activities.AnyAsync(a => a.RecipientId == recipientId
                    && a.ActorId == actorId && a.Type == type && a.PhotoId == photoId, ct)) return;

            db.Activities.Add(new Activity
            {
                RecipientId = recipientId, ActorId = actorId, Type = type, PhotoId = photoId, PostId = postId,
                Snippet = ActivityRules.Snippet(snippet), CreatedAt = clock.GetUtcNow(),
            });
            await db.SaveChangesAsync(ct);
        });

    /// <summary>Tells everyone following <paramref name="actorId"/> about a new photo or post (once per item).</summary>
    public Task NotifyFollowersAsync(Guid actorId, string type, Guid? photoId = null, Guid? postId = null, CancellationToken ct = default) =>
        SafelyAsync(type, async () =>
        {
            var followers = await db.Follows
                .Where(f => f.FolloweeId == actorId)
                .Where(f => !db.Blocks.Any(b => b.BlockerId == f.FollowerId && b.BlockedId == actorId))
                .Where(f => !db.ActivitySettings.Any(s => s.UserId == f.FollowerId && !s.FollowedPosts))
                .Where(f => !db.Activities.Any(a => a.RecipientId == f.FollowerId && a.Type == type
                                                    && a.PhotoId == photoId && a.PostId == postId))
                .Select(f => f.FollowerId)
                .ToListAsync(ct);
            if (followers.Count == 0) return;

            var now = clock.GetUtcNow();
            db.Activities.AddRange(followers.Select(id => new Activity
            {
                RecipientId = id, ActorId = actorId, Type = type, PhotoId = photoId, PostId = postId, CreatedAt = now,
            }));
            await db.SaveChangesAsync(ct);
        });

    /// <summary>Takes back an unread like or follow notification (after an unlike or unfollow).</summary>
    public Task RetractAsync(Guid recipientId, Guid actorId, string type, Guid? photoId = null, CancellationToken ct = default) =>
        SafelyAsync(type, () => db.Activities
            .Where(a => a.RecipientId == recipientId && a.ActorId == actorId && a.Type == type && a.PhotoId == photoId && a.ReadAt == null)
            .ExecuteDeleteAsync(ct));

    public async Task<ActivitySettingsDto> SettingsAsync(Guid userId, CancellationToken ct) =>
        await db.ActivitySettings.AsNoTracking().Where(s => s.UserId == userId)
            .Select(s => new ActivitySettingsDto(s.Likes, s.Comments, s.Follows, s.FollowedPosts, s.Challenges, s.Push))
            .SingleOrDefaultAsync(ct) ?? ActivitySettingsDto.AllOn;

    private async Task SafelyAsync(string type, Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Could not record {Type} activity", type);
            // Don't let a failed notification ride along with a later save in the same request.
            foreach (var entry in db.ChangeTracker.Entries<Activity>().Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
        }
    }
}
