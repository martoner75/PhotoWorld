using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Notifications;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Users;

namespace PhotoWorld.Web.Models;

public sealed record AccountSettingsViewModel(
    UserProfileDto Profile,
    NotificationsFormViewModel Notifications,
    EmailNotificationsDto? Current,
    IReadOnlyList<UserSummaryDto> Blocked,
    PhotoWorld.Shared.Activities.ActivitySettingsDto Activity);

/// <summary>The "Email notifications" form on the account settings page.</summary>
public sealed class NotificationsFormViewModel
{
    [Required(ErrorMessage = "Enter the email address to send notifications to.")]
    [EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
    [StringLength(NotificationLimits.EmailMax)]
    public string Email { get; set; } = "";

    public string Frequency { get; set; } = NotificationFrequency.Daily;

    public bool Enabled { get; set; }

    /// <summary>The weekly email about the member's own photos (views, likes, comments, followers) and walks.</summary>
    public bool WeeklySummary { get; set; }

    public static NotificationsFormViewModel From(EmailNotificationsDto? dto) => new()
    {
        Email = dto?.Email ?? dto?.AccountEmail ?? "",
        Frequency = dto?.Frequency ?? NotificationFrequency.Daily,
        Enabled = dto?.Enabled ?? false,
        WeeklySummary = dto?.WeeklySummary ?? false,
    };
}

public sealed record AdminUsersViewModel(PagedResult<PhotoWorld.Shared.Admin.AdminUserDto> Users, string? Search);
