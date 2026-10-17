using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Web.Authentication;

public static class AuthenticationSetup
{
    /// <summary>
    /// Cookie auth for the site, plus Google / Facebook / Instagram for registration and sign-in.
    /// A provider is only registered when its ClientId is configured, so the app runs without credentials.
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

        if (IsConfigured(AuthProviders.Facebook))
            auth.AddFacebook(AuthProviders.Facebook, o =>
            {
                Configure(o, section.GetSection(AuthProviders.Facebook));
                o.Fields.Add("picture");
            });

        if (IsConfigured(AuthProviders.Instagram))
            auth.AddOAuth<OAuthOptions, InstagramAuthenticationHandler>(AuthProviders.Instagram, AuthProviders.Instagram, o =>
            {
                Configure(o, section.GetSection(AuthProviders.Instagram));
                o.AuthorizationEndpoint = "https://www.instagram.com/oauth/authorize";
                o.TokenEndpoint = "https://api.instagram.com/oauth/access_token";
                o.CallbackPath = "/signin-instagram";
                o.Scope.Add("instagram_business_basic");
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
