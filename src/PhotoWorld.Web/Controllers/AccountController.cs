using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Notifications;
using PhotoWorld.Web.Authentication;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class AccountController(
    IAuthenticationSchemeProvider schemes,
    IPhotoWorldApiClient api,
    IMemoryCache cache,
    ILogger<AccountController> logger) : Controller
{
    private const string AppChallengeItem = "app_challenge";
    [AllowAnonymous]
    [HttpGet]
    public Task<IActionResult> Login(string? returnUrl, string? error) => ShowLogin(isRegister: false, returnUrl, error);

    /// <summary>Same flow as Login: the first sign-in with a provider creates the PhotoCommunity account.</summary>
    [AllowAnonymous]
    [HttpGet]
    public Task<IActionResult> Register(string? returnUrl, string? error) => ShowLogin(isRegister: true, returnUrl, error);

    [AllowAnonymous]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalLogin(string provider, string? returnUrl)
    {
        if (!AuthProviders.IsSupported(provider)) return BadRequest();
        provider = AuthProviders.Normalize(provider);
        if (await schemes.GetSchemeAsync(provider) is null)
            return RedirectToAction(nameof(Login), new { returnUrl, error = $"{provider} sign-in is not available yet." });

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(ExternalLoginCallback), new { returnUrl }),
            Items = { [AuthSchemes.ProviderItem] = provider },
        };
        return Challenge(properties, provider);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl, CancellationToken ct)
    {
        var (session, provider, error) = await CompleteExternalSignInAsync(ct);
        if (session is null) return RedirectToAction(nameof(Login), new { returnUrl, error });

        var user = session.User;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(PhotoWorldClaimTypes.Provider, provider!),
        ];
        if (user.Email is not null) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        if (user.AvatarUrl is not null) claims.Add(new Claim(PhotoWorldClaimTypes.AvatarUrl, user.AvatarUrl));
        // Only decides whether the menu shows "Admin"; the admin pages and the API check again on every request.
        if (user.IsAdmin) claims.Add(new Claim(PhotoWorldClaimTypes.IsAdmin, "true"));

        // The cookie lives exactly as long as the API token inside it.
        var properties = new AuthenticationProperties { IsPersistent = true, ExpiresUtc = session.ExpiresAt };
        properties.StoreTokens([new AuthenticationToken { Name = AuthSchemes.ApiAccessToken, Value = session.AccessToken }]);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            properties);

        logger.LogInformation("User {UserId} signed in with {Provider} (new member: {IsNewUser})",
            user.Id, provider, session.IsNewUser);

        TempData["Flash"] = session.IsNewUser
            ? $"Welcome to PhotoCommunity, {user.DisplayName}! Your account is ready."
            : $"Welcome back, {user.DisplayName}!";
        // New members get the welcome tour on the next page (see _Layout and js/tour.js).
        if (session.IsNewUser) TempData["Tour"] = "start";

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
    }

    // ----- Sign-in for the iPhone/iPad app (see AppSignIn) -----

    /// <summary>The app opens this in a secure browser sheet. Starts the provider sign-in for the app.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> AppLogin(string? provider, string? challenge)
    {
        if (!AppSignIn.IsWellFormedChallenge(challenge)) return BadRequest("Missing or malformed challenge.");
        provider = AuthProviders.IsSupported(provider ?? "") ? AuthProviders.Normalize(provider!) : AuthProviders.Google;
        if (await schemes.GetSchemeAsync(provider) is null) return AppRedirect(("error", $"{provider} sign-in is not available yet."));

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(AppLoginCallback)),
            Items = { [AuthSchemes.ProviderItem] = provider, [AppChallengeItem] = challenge },
        };
        return Challenge(properties, provider);
    }

    /// <summary>Back from the provider: park the session under a one-time code and hand the code to the app.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> AppLoginCallback(CancellationToken ct)
    {
        var external = await HttpContext.AuthenticateAsync(AuthSchemes.External);
        var challenge = external.Properties?.Items.TryGetValue(AppChallengeItem, out var c) == true ? c : null;
        var (session, provider, error) = await CompleteExternalSignInAsync(ct, external);
        if (session is null || !AppSignIn.IsWellFormedChallenge(challenge)) return AppRedirect(("error", error ?? "Sign-in failed."));

        var code = AppSignIn.NewCode();
        cache.Set(AppCodeKey(code), new PendingAppSignIn(session, challenge!), AppSignIn.CodeLifetime);
        logger.LogInformation("User {UserId} signed in to the app with {Provider} (new member: {IsNewUser})",
            session.User.Id, provider, session.IsNewUser);
        return AppRedirect(("code", code));
    }

    /// <summary>The app redeems its code (once) with the verifier it kept; only then does it get the token.</summary>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost]
    public IActionResult AppToken([FromBody] AppTokenRequest request)
    {
        var key = AppCodeKey(request.Code ?? "");
        if (!cache.TryGetValue(key, out PendingAppSignIn? pending) || pending is null) return BadRequest();
        cache.Remove(key); // one attempt per code, right or wrong
        if (!AppSignIn.VerifierMatches(request.Verifier, pending.Challenge))
        {
            logger.LogWarning("App sign-in code for user {UserId} presented with the wrong verifier", pending.Session.User.Id);
            return BadRequest();
        }
        return Ok(pending.Session);
    }

    private static string AppCodeKey(string code) => "app-signin:" + code;

    private RedirectResult AppRedirect(params (string Name, string Value)[] query) =>
        Redirect(AppSignIn.CallbackUri + QueryString.Create(query.Select(q => KeyValuePair.Create(q.Name, (string?)q.Value))));

    private sealed record PendingAppSignIn(AuthResponse Session, string Challenge);

    /// <summary>Reads the provider's result and exchanges its token for a PhotoCommunity session with the API.</summary>
    private async Task<(AuthResponse? Session, string? Provider, string? Error)> CompleteExternalSignInAsync(
        CancellationToken ct, AuthenticateResult? external = null)
    {
        external ??= await HttpContext.AuthenticateAsync(AuthSchemes.External);
        await HttpContext.SignOutAsync(AuthSchemes.External);

        var provider = external.Properties?.Items.TryGetValue(AuthSchemes.ProviderItem, out var p) == true ? p : null;
        // Apple: the API verifies the signed ID token (Apple's access token can't be checked by anyone else).
        var isApple = provider == AuthProviders.Apple;
        var providerToken = external.Properties?.GetTokenValue(isApple ? "id_token" : "access_token");
        if (!external.Succeeded || provider is null || providerToken is null)
        {
            logger.LogWarning("External sign-in with {Provider} failed: {Failure}", provider, external.Failure?.Message);
            return (null, provider, "Sign-in failed. Please try again.");
        }

        AuthResponse? session;
        try
        {
            session = await api.ExternalLoginAsync(isApple
                ? new ExternalLoginRequest(provider, providerToken,
                    external.Properties!.Items.TryGetValue(AuthSchemes.AppleNameItem, out var name) ? name : null,
                    external.Properties.GetTokenValue("refresh_token"))
                : new ExternalLoginRequest(provider, providerToken), ct);
        }
        catch (AccountSuspendedException ex)
        {
            return (null, provider, ex.Message + " If you think this is a mistake, contact us (see the Terms of Use).");
        }
        if (session is null)
        {
            logger.LogWarning("API rejected {Provider} sign-in", provider);
            return (null, provider, $"We couldn't verify your {provider} account.");
        }
        return (session, provider, null);
    }

    /// <summary>Kept for old links: your profile is now your member page.</summary>
    [HttpGet]
    public IActionResult Profile() => RedirectToAction("Me", "Members");

    [HttpGet]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var me = await api.GetCurrentUserAsync(ct);
        if (me is null) return RedirectToAction(nameof(Login));
        var current = await api.GetNotificationsAsync(ct);
        return View(new AccountSettingsViewModel(me, NotificationsFormViewModel.From(current), current,
            await api.GetBlockedMembersAsync(ct), await api.GetActivitySettingsAsync(ct)));
    }

    /// <summary>Saves the email notification settings (the form on the settings page).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Notifications([Bind(Prefix = nameof(AccountSettingsViewModel.Notifications))] NotificationsFormViewModel model,
        CancellationToken ct)
    {
        if (!NotificationFrequency.IsValid(model.Frequency))
            ModelState.AddModelError($"{nameof(AccountSettingsViewModel.Notifications)}.{nameof(model.Frequency)}", "Choose how often to get emails.");
        if (ModelState.IsValid)
        {
            var result = await api.SaveNotificationsAsync(new SaveEmailNotificationsRequest(model.Email, model.Frequency, model.Enabled, model.WeeklySummary), ct);
            if (result.Succeeded)
            {
                var saved = result.Value!;
                TempData["Flash"] = !saved.Enabled && !saved.WeeklySummary ? "Emails are off."
                    : saved.Confirmed ? $"Saved. We'll email {saved.Email}."
                    : $"Almost there: we've sent a confirmation link to {saved.Email}. Emails start once you click it.";
                return Redirect(Url.Action(nameof(Settings)) + "#notifications");
            }
            ModelState.AddApiErrors(result.Errors, new Dictionary<string, string>
            {
                ["email"] = $"{nameof(AccountSettingsViewModel.Notifications)}.{nameof(model.Email)}",
                ["frequency"] = $"{nameof(AccountSettingsViewModel.Notifications)}.{nameof(model.Frequency)}",
            });
        }

        var me = await api.GetCurrentUserAsync(ct);
        if (me is null) return RedirectToAction(nameof(Login));
        return View(nameof(Settings), new AccountSettingsViewModel(me, model, await api.GetNotificationsAsync(ct),
            await api.GetBlockedMembersAsync(ct), await api.GetActivitySettingsAsync(ct)));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(CancellationToken ct)
    {
        TempData["Flash"] = await api.ResendNotificationConfirmationAsync(ct) ?? "We've sent the confirmation link again.";
        return Redirect(Url.Action(nameof(Settings)) + "#notifications");
    }

    /// <summary>Sends the member's data as a JSON file ("Download my data" on the settings page). Read-only, so a plain GET.</summary>
    [HttpGet]
    public async Task<IActionResult> DownloadMyData(CancellationToken ct)
    {
        var data = await api.GetMyDataExportAsync(ct);
        if (data is null)
        {
            TempData["Flash"] = "We couldn't prepare your data. Try again in a moment.";
            return RedirectToAction(nameof(Settings));
        }
        logger.LogInformation("User {UserId} downloaded their data", User.FindFirstValue(ClaimTypes.NameIdentifier));
        return File(data, "application/json", $"photocommunity-my-data-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    /// <summary>Permanently deletes the account. The form requires typing DELETE, checked here too.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(string? confirmation, CancellationToken ct)
    {
        if (!string.Equals(confirmation?.Trim(), "DELETE", StringComparison.Ordinal))
        {
            TempData["Flash"] = "Type DELETE in capitals to confirm.";
            return RedirectToAction(nameof(Settings));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await api.DeleteAccountAsync(ct);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        logger.LogInformation("User {UserId} deleted their account", userId);

        TempData["Flash"] = "Your account and everything you shared have been deleted.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        logger.LogInformation("User {UserId} signed out", User.FindFirstValue(ClaimTypes.NameIdentifier));
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private async Task<IActionResult> ShowLogin(bool isRegister, string? returnUrl, string? error)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");

        var registered = (await schemes.GetRequestHandlerSchemesAsync()).Select(s => s.Name).ToHashSet();
        var providers = AuthProviders.All.Select(p => new ProviderOption(p, registered.Contains(p))).ToList();
        return View("Login", new LoginViewModel(isRegister, returnUrl, error, providers));
    }
}
