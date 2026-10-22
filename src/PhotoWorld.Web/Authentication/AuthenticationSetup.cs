using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Web.Authentication;

public static class AuthenticationSetup
{
    /// <summary>
    /// Cookie auth for the site, plus Google, Apple and LinkedIn for registration and sign-in. Each provider is only
    /// registered when its ClientId is configured, so the site runs without credentials.
    /// </summary>
    public static IServiceCollection AddPhotoWorldAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var auth = services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "PhotoWorld.Auth";
                o.LoginPath = "/Account/Login";
                o.LogoutPath = "/Account/Logout";
                o.AccessDeniedPath = "/Account/Login";
            })
            .AddCookie(AuthSchemes.External, o =>
            {
                o.Cookie.Name = AuthSchemes.External;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });

        var section = configuration.GetSection("Authentication");
        bool IsConfigured(string provider) => !string.IsNullOrWhiteSpace(section[$"{provider}:ClientId"]);

        if (IsConfigured(AuthProviders.Google))
            auth.AddGoogle(AuthProviders.Google, o => Configure(o, section.GetSection(AuthProviders.Google)));

        // "Sign In with LinkedIn using OpenID Connect" (the product to add to the LinkedIn app). The plain OAuth
        // handler is enough: the API verifies the access token and reads the profile itself.
        if (IsConfigured(AuthProviders.LinkedIn))
            auth.AddOAuth(AuthProviders.LinkedIn, o =>
            {
                Configure(o, section.GetSection(AuthProviders.LinkedIn));
                o.AuthorizationEndpoint = "https://www.linkedin.com/oauth/v2/authorization";
                o.TokenEndpoint = "https://www.linkedin.com/oauth/v2/accessToken";
                o.CallbackPath = "/signin-linkedin";
                o.UsePkce = false; // LinkedIn accepts PKCE from native apps only; this is a confidential web client.
                o.Scope.Clear();
                foreach (var scope in new[] { "openid", "profile", "email" }) o.Scope.Add(scope);
            });

        // Sign in with Apple: OpenID Connect, answered by a POST from appleid.apple.com (form_post), with a client
        // secret we sign per request with the team's key. The API verifies Apple's ID token itself.
        var apple = section.GetSection(AuthProviders.Apple);
        if (IsConfigured(AuthProviders.Apple) && !string.IsNullOrWhiteSpace(apple["TeamId"])
            && !string.IsNullOrWhiteSpace(apple["KeyId"]) && !string.IsNullOrWhiteSpace(apple["PrivateKey"]))
            auth.AddOpenIdConnect(AuthProviders.Apple, o =>
            {
                o.Authority = "https://appleid.apple.com";
                o.ClientId = apple["ClientId"];
                o.CallbackPath = "/signin-apple";
                o.SignInScheme = AuthSchemes.External;
                o.ResponseType = OpenIdConnectResponseType.Code;
                o.ResponseMode = OpenIdConnectResponseMode.FormPost; // required by Apple when asking for name or email
                o.UsePkce = false;
                o.GetClaimsFromUserInfoEndpoint = false; // Apple has none; everything is in the ID token
                o.SaveTokens = true;
                o.Scope.Clear();
                foreach (var scope in new[] { "openid", "name", "email" }) o.Scope.Add(scope);
                // Apple's answer is a cross-site POST, so the correlation and nonce cookies must be SameSite=None.
                o.CorrelationCookie.SameSite = o.NonceCookie.SameSite = SameSiteMode.None;
                o.CorrelationCookie.SecurePolicy = o.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Events.OnAuthorizationCodeReceived = ctx =>
                {
                    ctx.TokenEndpointRequest!.ClientSecret = AppleClientSecret.Create(apple["TeamId"]!, apple["KeyId"]!,
                        apple["ClientId"]!, apple["PrivateKey"]!, DateTimeOffset.UtcNow);
                    return Task.CompletedTask;
                };
                o.Events.OnTokenValidated = async ctx =>
                {
                    // Apple sends the member's name only on their first sign-in, as a "user" form field.
                    if (ctx.Request.HasFormContentType && (await ctx.Request.ReadFormAsync()).TryGetValue("user", out var user)
                        && AppleName(user.ToString()) is { } name)
                        ctx.Properties!.Items[AuthSchemes.AppleNameItem] = name;
                };
                o.Events.OnRemoteFailure = ctx => BackToLogin(ctx);
            });

        return services;
    }

    private static void Configure(OAuthOptions o, IConfiguration providerSection)
    {
        providerSection.Bind(o);
        o.SignInScheme = AuthSchemes.External;
        // Keep the provider access token so the API can verify it independently.
        o.SaveTokens = true;
        o.Events.OnRemoteFailure = ctx => BackToLogin(ctx);
    }

    /// <summary>User cancelled or the provider returned an error: back to the login page instead of an exception.</summary>
    private static Task BackToLogin(RemoteFailureContext ctx)
    {
        var message = Uri.EscapeDataString(ctx.Failure?.Message ?? "Sign-in was cancelled.");
        ctx.Response.Redirect($"/Account/Login?error={message}");
        ctx.HandleResponse();
        return Task.CompletedTask;
    }

    /// <summary>"Ana Silva" from Apple's {"name":{"firstName":"Ana","lastName":"Silva"}}, or null.</summary>
    internal static string? AppleName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("name", out var n)) return null;
            string? Part(string key) => n.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;
            var full = string.Join(' ', new[] { Part("firstName"), Part("lastName") }.Where(p => !string.IsNullOrEmpty(p)));
            return full.Length == 0 ? null : full[..Math.Min(full.Length, 100)];
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
