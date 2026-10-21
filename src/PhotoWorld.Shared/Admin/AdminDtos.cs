using System.ComponentModel.DataAnnotations;

namespace PhotoWorld.Shared.Admin;

public static class AdminLimits
{
    public const int SuspensionReasonMax = 500;
    public const int SearchMax = 100;
}

/// <summary>A member as admins see them on the Users page (includes the email address, for contacting them).</summary>
public sealed record AdminUserDto(
    Guid Id,
    string DisplayName,
    string? Email,
    string? AvatarUrl,
    DateTimeOffset MemberSince,
    DateTimeOffset LastLoginAt,
    int PhotoCount,
    int PostCount,
    bool IsAdmin,
    DateTimeOffset? SuspendedAt,
    string? SuspensionReason);

public sealed record SetAdminRequest(bool IsAdmin);

/// <summary>Suspends (with a reason shown to the member when they try to sign in) or reactivates an account.</summary>
public sealed record SetSuspensionRequest(bool Suspended, [StringLength(AdminLimits.SuspensionReasonMax)] string? Reason);
