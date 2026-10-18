using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

/// <summary>
/// The links in notification emails. They work without signing in (the email may be opened anywhere); the secret
/// token in the link is what authorises them.
/// </summary>
[AllowAnonymous]
public class NotificationsController(IPhotoWorldApiClient api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Confirm(string? token, CancellationToken ct) =>
        View(!string.IsNullOrWhiteSpace(token) && await api.ConfirmNotificationsAsync(token, ct));

    /// <summary>Asks first, so link scanners that open every URL in an email don't unsubscribe anyone.</summary>
    [HttpGet]
    public IActionResult Unsubscribe(string? token) => View(model: token);

    /// <summary>
    /// The button on the page above, and RFC 8058 one-click unsubscribe: email apps POST
    /// "List-Unsubscribe=One-Click" to the link from the List-Unsubscribe header, without a form token.
    /// </summary>
    [HttpPost, IgnoreAntiforgeryToken]
    [ActionName(nameof(Unsubscribe))]
    public async Task<IActionResult> UnsubscribePost(string? token, CancellationToken ct) =>
        View("Unsubscribed", !string.IsNullOrWhiteSpace(token) && await api.UnsubscribeAsync(token, ct));
}
