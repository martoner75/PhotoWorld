using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.Api.Services.Places;

/// <summary>
/// Every hour: for places with alerts on where it's now the early evening, rates tomorrow's golden hours and tells the
/// owner when one looks good. Each place and day is claimed by inserting its notice first, so an alert goes out once
/// even with several API instances. PushDispatcher sends it to their phones like any other activity.
/// </summary>
public sealed class ForecastSender(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ForecastSender> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Light forecast run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoWorldDbContext>();
        var weather = scope.ServiceProvider.GetRequiredService<OpenMeteoClient>();
        var now = clock.GetUtcNow();

        var places = await db.Places.AsNoTracking()
            .Where(p => p.ForecastAlerts && p.User.SuspendedAt == null)
            .Where(p => !db.ActivitySettings.Any(s => s.UserId == p.UserId && !s.Forecasts))
            .Select(p => new { p.Id, p.UserId, p.Name, p.Latitude, p.Longitude })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var p in places.Where(p => ForecastRules.IsAlertHour(now, p.Longitude)))
        {
            var date = ForecastRules.Tomorrow(now, p.Longitude);
            if (await db.PlaceForecastNotices.AnyAsync(n => n.PlaceId == p.Id && n.Date == date, ct)) continue;

            IReadOnlyList<HourlyWeather> hours;
            try
            {
                hours = await weather.HourlyAsync(p.Latitude, p.Longitude, ct);
            }
            catch (ForecastUnavailableException ex)
            {
                logger.LogWarning(ex, "No forecast for place {PlaceId}; trying again next run", p.Id);
                continue;
            }

            var best = ForecastRules.WorthTelling(LightForecast.Windows(date, 1, p.Latitude, p.Longitude, hours, now));
            if (best is null) continue;

            // The claim: a second instance's insert hits the key and skips.
            db.PlaceForecastNotices.Add(new PlaceForecastNotice { PlaceId = p.Id, Date = date, SentAt = now });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                continue;
            }

            db.Activities.Add(new Activity
            {
                RecipientId = p.UserId, Type = ActivityTypes.LightForecast, Snippet = ForecastRules.Snippet(p.Name, best), CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);
            sent++;
        }
        if (sent > 0) logger.LogInformation("Sent {Count} light forecast alerts", sent);

        await db.PlaceForecastNotices.Where(n => n.SentAt < now - ForecastRules.KeepNoticesFor).ExecuteDeleteAsync(ct);
    }
}
