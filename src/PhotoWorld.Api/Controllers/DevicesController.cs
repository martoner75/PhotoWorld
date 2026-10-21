using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Activities;

namespace PhotoWorld.Api.Controllers;

/// <summary>The app registers its push token after sign-in and forgets it on sign-out.</summary>
[ApiController]
[Authorize]
public class DevicesController(PhotoWorldDbContext db, TimeProvider clock) : ControllerBase
{
    /// <summary>Registers (or moves to the caller) a phone's push token.</summary>
    [HttpPut(ApiRoutes.Devices)]
    public async Task<IActionResult> Register(RegisterDeviceRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        if (request.Platform is not (DevicePlatforms.Ios or DevicePlatforms.Android))
            return Problem("Platform must be ios or android.", statusCode: StatusCodes.Status400BadRequest);

        var now = clock.GetUtcNow();
        var token = request.Token.Trim();
        var device = await db.Devices.SingleOrDefaultAsync(d => d.Token == token, ct);
        if (device is null) db.Devices.Add(device = new DeviceRegistration { Token = token, Platform = request.Platform, CreatedAt = now });
        // The same phone signed in as someone else now: it belongs to them.
        (device.UserId, device.Platform, device.LastSeenAt) = (me, request.Platform, now);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* registered twice at the same moment: it's there */ }
        return NoContent();
    }

    [HttpDelete(ApiRoutes.Devices)]
    public async Task<IActionResult> Forget([FromQuery] string token, CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();
        await db.Devices.Where(d => d.Token == token && d.UserId == me).ExecuteDeleteAsync(ct);
        return NoContent();
    }
}
