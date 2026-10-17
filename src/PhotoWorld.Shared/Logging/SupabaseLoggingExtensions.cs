using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace PhotoWorld.Shared.Logging;

public static class SupabaseLoggingExtensions
{
    /// <summary>
    /// Sends logs to the Supabase <c>photoworld.app_logs</c> table, alongside the default console logging.
    /// Does nothing (with a console notice) when <paramref name="connectionString"/> is empty,
    /// so the app still runs locally without a database.
    /// </summary>
    public static ILoggingBuilder AddSupabase(this ILoggingBuilder builder, string? connectionString, string application)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine($"[SupabaseLogger] {application}: no logging connection string set, Supabase logging is off.");
            return builder;
        }

        builder.Services.Configure<SupabaseLoggerOptions>(o =>
        {
            o.ConnectionString = connectionString;
            o.Application = application;
        });
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, SupabaseLoggerProvider>());
        return builder;
    }
}
