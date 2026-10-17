using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Auth;
using PhotoWorld.Web.Authentication;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class AccountController(
    IAuthenticationSchemeProvider schemes,
    IPhotoWorldApiClient api,
    ILogger<AccountController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public Task<IActionResult> Login(string? returnUrl, string? error) => ShowLogin(isRegister: false, returnUrl, error);

    /// <summary>Same flow as Login: the first sign-in with a provider creates the PhotoWorld account.</summary>
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
        var external = await HttpContext.AuthenticateAsync(AuthSchemes.External);
        await HttpContext.SignOutAsync(AuthSchemes.External);

        var provider = external.Properties?.Items.TryGetValue(AuthSchemes.ProviderItem, out var p) == true ? p : null;
        var providerToken = external.Properties?.GetTokenValue("access_token");
        if (!external.Succeeded || provider is null || providerToken is null)
        {
            logger.LogWarning("External sign-in with {Provider} failed: {Failure}", provider, external.Failure?.Message);
            return RedirectToAction(nameof(Login), new { returnUrl, error = "Sign-in failed. Please try again." });
        }

        var session = await api.ExternalLoginAsync(new ExternalLoginRequest(provider, providerToken), ct);
        if (session is null)
        {
            logger.LogWarning("API rejected {Provider} sign-in", provider);
            return RedirectToAction(nameof(Login), new { returnUrl, error = $"We couldn't verify your {provider} account." });
        }

        var user = session.User;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(PhotoWorldClaimTypes.Provider, provider),
        ];
        if (user.Email is not null) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        if (user.AvatarUrl is not null) claims.Add(new Claim(PhotoWorldClaimTypes.AvatarUrl, user.AvatarUrl));

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
            ? $"Welcome to PhotoWorld, {user.DisplayName}! Your account is ready."
            : $"Welcome back, {user.DisplayName}!";

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
    }

    /// <summary>Kept for old links: your profile is now your member page.</summary>
    [HttpGet]
    public IActionResult Profile() => RedirectToAction("Me", "Members");

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
