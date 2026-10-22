using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Web.Authentication;

public static class AuthenticationSetup
{
    /// <summary>
    /// Cookie auth for the site, plus Google and LinkedIn for registration and sign-in. Each provider is only
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

        return services;
    }

    private static void Configure(OAuthOptions o, IConfiguration providerSection)
    {
        providerSection.Bind(o);
        o.SignInScheme = AuthSchemes.External;
        // Keep the provider access token so the API can verify it independently.
        o.SaveTokens = true;
        o.Events.OnRemoteFailure = ctx =>
        {
            // User cancelled or the provider returned an error: back to the login page instead of an exception.
            var message = Uri.EscapeDataString(ctx.Failure?.Message ?? "Sign-in was cancelled.");
            ctx.Response.Redirect($"/Account/Login?error={message}");
            ctx.HandleResponse();
            return Task.CompletedTask;
        };
    }
}
