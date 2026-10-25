using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.Api.Services.Places;

/// <summary>Thrown when the weather service can't be reached or answers with something unexpected.</summary>
public sealed class ForecastUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Hourly cloud and rain forecasts from Open-Meteo (free for non-commercial use, no key; CC BY 4.0, so pages showing
/// it credit Open-Meteo.com). Only coordinates rounded to about 1 km are sent: never a place's name or whose it is.
/// Answers are cached for an hour per rounded location, so many places nearby cost one call.
/// </summary>
public sealed class OpenMeteoClient(HttpClient http, IMemoryCache cache, ILogger<OpenMeteoClient> logger)
{
    private const string Endpoint = "https://api.open-meteo.com/v1/forecast";
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

    /// <summary>Enough days that tomorrow's and the day after's golden hours are covered at any longitude.</summary>
    private const int ForecastDays = LightForecast.DaysAhead + 1;

    public Task<IReadOnlyList<HourlyWeather>> HourlyAsync(double latitude, double longitude, CancellationToken ct)
    {
        var (lat, lon) = (Round(latitude), Round(longitude));
        return cache.GetOrCreateAsync($"open-meteo:{lat}:{lon}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            var url = $"{Endpoint}?latitude={lat}&longitude={lon}" +
                      "&hourly=cloud_cover_low,cloud_cover_mid,cloud_cover_high,precipitation_probability" +
                      $"&forecast_days={ForecastDays}&timezone=GMT&timeformat=unixtime";
            try
            {
                using var response = await http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                    throw new ForecastUnavailableException($"Open-Meteo answered {(int)response.StatusCode}.");
                return Parse(await response.Content.ReadAsStringAsync(ct));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException { CancellationToken.IsCancellationRequested: false })
            {
                logger.LogWarning(ex, "Open-Meteo request failed");
                throw new ForecastUnavailableException("The weather service couldn't be reached.", ex);
            }
        })!;
    }

    /// <summary>Reads Open-Meteo's column-per-variable hourly JSON. Missing values count as 0.</summary>
    internal static IReadOnlyList<HourlyWeather> Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var hourly = doc.RootElement.GetProperty("hourly");
            var times = hourly.GetProperty("time").EnumerateArray().Select(t => t.GetInt64()).ToList();
            int[] Column(string name) => hourly.TryGetProperty(name, out var values)
                ? values.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Number ? (int)Math.Round(v.GetDouble()) : 0).ToArray()
                : new int[times.Count];
            var (low, mid, high, rain) = (Column("cloud_cover_low"), Column("cloud_cover_mid"),
                Column("cloud_cover_high"), Column("precipitation_probability"));

            return times.Select((t, i) => new HourlyWeather(DateTimeOffset.FromUnixTimeSeconds(t),
                At(low, i), At(mid, i), At(high, i), At(rain, i))).ToList();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new ForecastUnavailableException("The weather service sent a forecast we couldn't read.", ex);
        }
    }

    private static int At(int[] column, int i) => i < column.Length ? Math.Clamp(column[i], 0, 100) : 0;

    /// <summary>Two decimals: about 1 km, plenty for cloud cover and vague enough to keep a pin private.</summary>
    private static string Round(double degrees) => Math.Round(degrees, 2).ToString("0.00", CultureInfo.InvariantCulture);
}
