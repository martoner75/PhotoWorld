namespace PhotoWorld.Api.Services;

/// <summary>
/// Guard rails on the Users page, kept apart so they're easy to test. Each returns why not, or null if allowed. Because
/// nobody can remove their own admin rights or suspend themselves, the admin acting always remains, so there's
/// always at least one admin.
/// </summary>
public static class AdminRules
{
    public static string? CannotChangeAdmin(Guid? me, Guid target, bool makeAdmin, bool targetSuspended) =>
        target == me && !makeAdmin ? "You can't remove your own admin rights. Ask another admin."
        : makeAdmin && targetSuspended ? "Reactivate the account before making them an admin."
        : null;

    public static string? CannotSuspend(Guid? me, Guid target, bool suspend) =>
        suspend && target == me ? "You can't suspend your own account." : null;
}
