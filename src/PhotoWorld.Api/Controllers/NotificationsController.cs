using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Notifications;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Notifications;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// Email notifications about new blog posts. Members manage their own settings; the confirm and unsubscribe
/// endpoints are anonymous because they're opened from emails, and are authorised by the secret in the link.
/// </summary>
[ApiController]
public class NotificationsController(
    PhotoWorldDbContext db,
    IEmailSender sender,
    NotificationEmails emails,
    TimeProvider clock,
    ILogger<NotificationsController> logger) : ControllerBase
{
    [Authorize]
    [HttpGet(ApiRoutes.MyNotifications)]
    public async Task<ActionResult<EmailNotificationsDto>> Get(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var accountEmail = await db.Users.Where(u => u.Id == me).Select(u => u.Email).SingleOrDefaultAsync(ct);
        var sub = await db.EmailSubscriptions.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == me, ct);
        return ToDto(sub, accountEmail);
    }

    [Authorize]
    [HttpPut(ApiRoutes.MyNotifications)]
    public async Task<ActionResult<EmailNotificationsDto>> Save(SaveEmailNotificationsRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (!NotificationFrequency.IsValid(request.Frequency))
        {
            ModelState.AddModelError(nameof(request.Frequency), "Choose how often to get emails.");
            return ValidationProblem(ModelState);
        }

        var now = clock.GetUtcNow();
        var email = request.Email.Trim();
        var accountEmail = await db.Users.Where(u => u.Id == me).Select(u => u.Email).SingleAsync(ct);
        var sub = await db.EmailSubscriptions.SingleOrDefaultAsync(s => s.UserId == me, ct);
        var isNew = sub is null;
        if (sub is null)
        {
            sub = new EmailSubscription
            {
                UserId = me, Email = email, Frequency = request.Frequency,
                UnsubscribeToken = NotificationRules.NewToken(), CreatedAt = now,
            };
            db.EmailSubscriptions.Add(sub);
        }
        if (isNew || !string.Equals(sub.Email, email, StringComparison.OrdinalIgnoreCase)) SetAddress(sub, email, accountEmail, now);

        // Switching on (or back on) starts from now: no backlog of older posts. A new frequency starts a new period.
        if (request.Enabled && (isNew || !sub.Enabled)) StartFrom(sub, now);
        if (sub.Frequency != request.Frequency) sub.PeriodStartedAt = now;
        sub.Frequency = request.Frequency;
        sub.Enabled = request.Enabled;
        // The first summary covers a whole week from switching on.
        if (request.WeeklySummary && (isNew || !sub.WeeklySummary)) sub.WeeklySummaryFrom = now;
        sub.WeeklySummary = request.WeeklySummary;
        sub.UpdatedAt = now;

        var token = sub.ConfirmedAt is null && (sub.Enabled || sub.WeeklySummary) ? IssueConfirmToken(sub, now) : null;
        await db.SaveChangesAsync(ct);

        if (token is not null && !await TrySendConfirmationAsync(sub.Email, token, ct))
        {
            return Problem("Your settings are saved, but we couldn't send the confirmation email. Try “Send the link again” in a moment.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        return ToDto(sub, accountEmail);
    }

    [Authorize]
    [HttpPost(ApiRoutes.MyNotificationsConfirmation)]
    public async Task<IActionResult> ResendConfirmation(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        var sub = await db.EmailSubscriptions.SingleOrDefaultAsync(s => s.UserId == me, ct);
        if (sub is null || sub.ConfirmedAt is not null) return NotFound();

        var token = IssueConfirmToken(sub, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await TrySendConfirmationAsync(sub.Email, token, ct)
            ? NoContent()
            : Problem("We couldn't send the confirmation email. Try again in a moment.", statusCode: StatusCodes.Status502BadGateway);
    }

    [AllowAnonymous]
    [HttpPost(ApiRoutes.ConfirmNotifications)]
    public async Task<IActionResult> Confirm(NotificationTokenRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = NotificationRules.Hash(request.Token);
        var sub = await db.EmailSubscriptions.SingleOrDefaultAsync(s => s.ConfirmTokenHash == hash, ct);
        if (sub is null || sub.ConfirmTokenExpiresAt < now) return NotFound();

        sub.ConfirmedAt = now;
        (sub.ConfirmTokenHash, sub.ConfirmTokenExpiresAt) = (null, null);
        StartFrom(sub, now);
        sub.WeeklySummaryFrom = now;
        sub.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Member {UserId} confirmed their notification email", sub.UserId);
        return NoContent();
    }

    /// <summary>One-click unsubscribe from an email: stops every PhotoWorld email. Idempotent; unknown tokens answer 404.</summary>
    [AllowAnonymous]
    [HttpPost(ApiRoutes.Unsubscribe)]
    public async Task<IActionResult> Unsubscribe(NotificationTokenRequest request, CancellationToken ct)
    {
        var sub = await db.EmailSubscriptions.SingleOrDefaultAsync(s => s.UnsubscribeToken == request.Token, ct);
        if (sub is null) return NotFound();
        if (sub.Enabled || sub.WeeklySummary)
        {
            (sub.Enabled, sub.WeeklySummary) = (false, false);
            sub.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Member {UserId} unsubscribed from emails by email link", sub.UserId);
        }
        return NoContent();
    }

    /// <summary>A new address: the verified Google one is trusted as is; anything else waits for its confirmation link.</summary>
    private static void SetAddress(EmailSubscription sub, string email, string? accountEmail, DateTimeOffset now)
    {
        sub.Email = email;
        sub.ConfirmedAt = NotificationRules.IsAccountEmail(email, accountEmail) ? now : null;
        (sub.ConfirmTokenHash, sub.ConfirmTokenExpiresAt) = (null, null);
        StartFrom(sub, now);
    }

    private static void StartFrom(EmailSubscription sub, DateTimeOffset now) => (sub.NotifiedThrough, sub.PeriodStartedAt) = (now, now);

    private static string IssueConfirmToken(EmailSubscription sub, DateTimeOffset now)
    {
        var token = NotificationRules.NewToken();
        sub.ConfirmTokenHash = NotificationRules.Hash(token);
        sub.ConfirmTokenExpiresAt = now + NotificationRules.ConfirmLinkLifetime;
        return token;
    }

    private async Task<bool> TrySendConfirmationAsync(string email, string token, CancellationToken ct)
    {
        try
        {
            await sender.SendAsync(emails.Confirmation(email, token), ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not send a notification confirmation email");
            return false;
        }
    }

    private static EmailNotificationsDto ToDto(EmailSubscription? sub, string? accountEmail) => sub is null
        ? new EmailNotificationsDto(null, NotificationFrequency.Daily, false, false, accountEmail)
        : new EmailNotificationsDto(sub.Email, sub.Frequency, sub.Enabled, sub.ConfirmedAt is not null, accountEmail, sub.WeeklySummary);
}
