namespace PhotoWorld.Api.Data;

/// <summary>
/// A row in <c>photoworld.app_logs</c>, written by both the API and the Web UI through
/// <see cref="PhotoWorld.Shared.Logging.SupabaseLoggerProvider"/>. Mapped here only so EF migrations
/// own the table; keep the columns in sync with the provider's COPY statement.
/// </summary>
public class LogEntry
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string Level { get; set; }
    public required string Category { get; set; }
    public required string Message { get; set; }
    public string? Exception { get; set; }
    public int EventId { get; set; }
    public string? EventName { get; set; }
    public required string Application { get; set; }
    public required string Environment { get; set; }
    public required string MachineName { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? Properties { get; set; }
}
