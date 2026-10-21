using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Identity;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Api.Controllers;

[ApiController]
public class AuthController(
    PhotoWorldDbContext db,
    IEnumerable<IExternalIdentityVerifier> verifiers,
    JwtTokenService tokens,
    AdminAccess admins,
    TimeProvider clock,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Signs a user in with a provider access token, registering them on first visit.
    /// </summary>
    [HttpPost(ApiRoutes.ExternalLogin)]
    public async Task<ActionResult<AuthResponse>> ExternalLogin(ExternalLoginRequest request, CancellationToken ct)
    {
        if (!AuthProviders.IsSupported(request.Provider))
            return Problem($"Unsupported provider '{request.Provider}'.", statusCode: StatusCodes.Status400BadRequest);

        var provider = AuthProviders.Normalize(request.Provider);
        var verifier = verifiers.Single(v => v.Provider == provider);

        ExternalIdentity? identity;
        try
        {
            identity = await verifier.VerifyAsync(request.AccessToken, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "{Provider} token validation failed", provider);
            identity = null;
        }
        if (identity is null)
            return Problem($"The {provider} sign-in could not be verified.", statusCode: StatusCodes.Status401Unauthorized);

        var now = clock.GetUtcNow();
        var login = await db.ExternalLogins
            .Include(l => l.User).ThenInclude(u => u.ExternalLogins)
            .SingleOrDefaultAsync(l => l.Provider == provider && l.ProviderUserId == identity.ProviderUserId, ct);

        var isNewUser = login is null;
        User user;
        if (login is not null)
        {
            user = login.User;
            // Keep the profile fresh with whatever the provider tells us now.
            user.DisplayName = identity.DisplayName;
            user.Email ??= identity.Email;
            user.AvatarUrl = identity.AvatarUrl ?? user.AvatarUrl;
        }
        else
        {
            // Same verified email from a different provider = same person; link instead of duplicating.
            var existing = identity.Email is null ? null : await db.Users.Include(u => u.ExternalLogins)
                .FirstOrDefaultAsync(u => u.Email == identity.Email, ct);
            isNewUser = existing is null;
            user = existing ?? db.Users.Add(new User
            {
                DisplayName = identity.DisplayName,
                Email = identity.Email,
                AvatarUrl = identity.AvatarUrl,
                CreatedAt = now,
            }).Entity;
            user.ExternalLogins.Add(new ExternalLogin { Provider = provider, ProviderUserId = identity.ProviderUserId });
        }
        if (user.SuspendedAt is not null)
        {
            logger.LogWarning("Suspended member {UserId} tried to sign in", user.Id);
            return Problem(SessionCheck.SuspendedMessage(user.SuspensionReason), statusCode: StatusCodes.Status403Forbidden);
        }
        user.LastLoginAt = now;
        await db.SaveChangesAsync(ct);

        if (isNewUser) logger.LogInformation("New PhotoWorld member {UserId} registered via {Provider}", user.Id, provider);

        var (token, expiresAt) = tokens.Issue(user, provider);
        return new AuthResponse(token, expiresAt, isNewUser, user.ToDto() with { IsAdmin = await admins.IsAdminAsync(user.Id, ct) });
    }
}
