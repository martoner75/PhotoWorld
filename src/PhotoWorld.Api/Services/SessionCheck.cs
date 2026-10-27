using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PhotoWorld.Api.Data;

namespace PhotoWorld.Api.Services;

/// <summary>Rejects valid tokens of members who were suspended (or deleted) after the token was issued.</summary>
public static class SessionCheck
{
    /// <summary>How long a member's "still active" answer is cached; a suspension takes effect within this time.</summary>
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    public static string SuspendedMessage(string? reason) =>
        "This PhotoCommunity account has been suspended" + (string.IsNullOrWhiteSpace(reason) ? "." : $": {reason.Trim().TrimEnd('.')}.");

    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        if (context.Principal?.GetUserId() is not { } id)
        {
            context.Fail("No member id in the token.");
            return;
        }
        var services = context.HttpContext.RequestServices;
        var active = await services.GetRequiredService<IMemoryCache>().GetOrCreateAsync($"active-member:{id}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return await services.GetRequiredService<PhotoWorldDbContext>().Users
                .AnyAsync(u => u.Id == id && u.SuspendedAt == null, context.HttpContext.RequestAborted);
        });
        if (!active) context.Fail("The account is suspended or no longer exists.");
    }

    /// <summary>Called when an admin suspends or reactivates someone, so it applies straight away on this instance.</summary>
    public static void Forget(IMemoryCache cache, Guid userId) => cache.Remove($"active-member:{userId}");
}
