namespace PhotoWorld.Shared.Logging;

public sealed class SupabaseLoggerOptions
{
    /// <summary>Postgres connection string for the Supabase database (Session pooler).</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>Written to <c>app_logs.application</c> so API and Web rows can be told apart.</summary>
    public string Application { get; set; } = "PhotoWorld";

    public string Environment { get; set; } =
        System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? "Production";

    public int BatchSize { get; set; } = 200;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Entries beyond this many waiting to be written are dropped rather than blocking the app.</summary>
    public int QueueCapacity { get; set; } = 10_000;
}
