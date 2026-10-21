using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;

namespace PhotoWorld.Api.Services;

/// <summary>
/// Who may use the admin pages: members with <c>users.is_admin</c> set (and not suspended). Admins are managed in
/// the database only: on the Users page, or with SQL in the Supabase dashboard.
/// </summary>
public sealed class AdminAccess(PhotoWorldDbContext db)
{
    public async Task<bool> IsAdminAsync(Guid? userId, CancellationToken ct) =>
        userId is { } id && await db.Users.AnyAsync(u => u.Id == id && u.IsAdmin && u.SuspendedAt == null, ct);
}
