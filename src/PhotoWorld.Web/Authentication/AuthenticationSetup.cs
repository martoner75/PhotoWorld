using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Web.Authentication;

public static class AuthenticationSetup
{
    /// <summary>
    /// Cookie auth for the site, plus Google for registration and sign-in.
    /// Google is only registered when its ClientId is configured, so the app runs without credentials.
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
