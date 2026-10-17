using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace PhotoWorld.Shared.Logging;

/// <summary>
/// Writes log entries to <c>photoworld.app_logs</c> in Supabase. Logging never waits on the database:
/// entries go into an in-memory queue and a background loop bulk-inserts them in batches with COPY.
/// If the database is unreachable, the batch is dropped and the failure is printed to stderr.
/// </summary>
[ProviderAlias("Supabase")]
public sealed class SupabaseLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    // Keep in sync with PhotoWorld.Api/Data/LogEntry.cs (the EF migration owns the table).
    private const string CopyCommand =
        "COPY photoworld.app_logs (timestamp, level, category, message, exception, event_id, event_name, " +
        "application, environment, machine_name, trace_id, span_id, properties) FROM STDIN (FORMAT BINARY)";

    private readonly SupabaseLoggerOptions _options;
    private readonly NpgsqlDataSource _dataSource;
    private readonly Channel<LogRow> _queue;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _writer;
    private IExternalScopeProvider? _scopes;

    public SupabaseLoggerProvider(IOptions<SupabaseLoggerOptions> options)
    {
        _options = options.Value;
        _dataSource = NpgsqlDataSource.Create(_options.ConnectionString);
        _queue = Channel.CreateBounded<LogRow>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });
        _writer = Task.Run(WriteLoopAsync);
    }

    public ILogger CreateLogger(string categoryName) => new SupabaseLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
        // Flush what's queued, but don't hold up shutdown for long if Supabase is slow.
        _queue.Writer.TryComplete();
        _stopping.Cancel();
        _writer.Wait(TimeSpan.FromSeconds(5));
        _dataSource.Dispose();
        _stopping.Dispose();
    }

    private void Enqueue(LogRow row) => _queue.Writer.TryWrite(row);

    private async Task WriteLoopAsync()
    {
        var batch = new List<LogRow>(_options.BatchSize);
        var reader = _queue.Reader;

        while (await reader.WaitToReadAsync())
        {
            while (batch.Count < _options.BatchSize && reader.TryRead(out var row)) batch.Add(row);

            try
            {
                await WriteBatchAsync(batch);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SupabaseLogger] Dropped {batch.Count} log entries: {ex.Message}");
            }
            batch.Clear();

            // Let entries accumulate between round-trips, unless we're draining for shutdown.
            if (!_stopping.IsCancellationRequested)
            {
                try { await Task.Delay(_options.FlushInterval, _stopping.Token); }
                catch (OperationCanceledException) { }
            }
        }
    }

    private async Task WriteBatchAsync(List<LogRow> batch)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var importer = await connection.BeginBinaryImportAsync(CopyCommand);
        foreach (var r in batch)
        {
            await importer.StartRowAsync();
            await importer.WriteAsync(r.Timestamp, NpgsqlDbType.TimestampTz);
            await importer.WriteAsync(r.Level, NpgsqlDbType.Varchar);
            await importer.WriteAsync(r.Category, NpgsqlDbType.Varchar);
            await importer.WriteAsync(r.Message, NpgsqlDbType.Text);
            await WriteNullableAsync(importer, r.Exception, NpgsqlDbType.Text);
            await importer.WriteAsync(r.EventId, NpgsqlDbType.Integer);
            await WriteNullableAsync(importer, r.EventName, NpgsqlDbType.Varchar);
            await importer.WriteAsync(_options.Application, NpgsqlDbType.Varchar);
            await importer.WriteAsync(_options.Environment, NpgsqlDbType.Varchar);
            await importer.WriteAsync(System.Environment.MachineName, NpgsqlDbType.Varchar);
            await WriteNullableAsync(importer, r.TraceId, NpgsqlDbType.Varchar);
            await WriteNullableAsync(importer, r.SpanId, NpgsqlDbType.Varchar);
            await WriteNullableAsync(importer, r.Properties, NpgsqlDbType.Jsonb);
        }
        await importer.CompleteAsync();
    }

    private static Task WriteNullableAsync(NpgsqlBinaryImporter importer, string? value, NpgsqlDbType type) =>
        value is null ? importer.WriteNullAsync() : importer.WriteAsync(value, type);

    private sealed record LogRow(
        DateTimeOffset Timestamp, string Level, string Category, string Message, string? Exception,
        int EventId, string? EventName, string? TraceId, string? SpanId, string? Properties);

    private sealed class SupabaseLogger(string category, SupabaseLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            provider._scopes?.Push(state);

        // Level filtering comes from the "Logging:Supabase" configuration section.
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception is null) return;

            // Structured values from the message template plus ambient scopes (RequestPath, RequestId, ...).
            var properties = new Dictionary<string, object?>();
            provider._scopes?.ForEachScope(static (scope, props) => AddProperties(scope, props), properties);
            AddProperties(state, properties);

            var activity = Activity.Current;
            provider.Enqueue(new LogRow(
                DateTimeOffset.UtcNow,
                logLevel.ToString(),
                category,
                message,
                exception?.ToString(),
                eventId.Id,
                eventId.Name,
                activity?.TraceId.ToHexString(),
                activity?.SpanId.ToHexString(),
                properties.Count > 0 ? JsonSerializer.Serialize(properties) : null));
        }

        private static void AddProperties(object? state, Dictionary<string, object?> properties)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> pairs) return;
            foreach (var (key, value) in pairs)
            {
                if (key == "{OriginalFormat}") continue;
                // Only primitives are serialized as-is; anything else is stored as its string form.
                properties[key] = value is null or string or bool or Guid or DateTime or DateTimeOffset or TimeSpan
                    || value.GetType().IsPrimitive || value is decimal
                    ? value
                    : value.ToString();
            }
        }
    }
}
