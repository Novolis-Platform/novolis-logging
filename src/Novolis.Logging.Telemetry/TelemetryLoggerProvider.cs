using Microsoft.Extensions.Logging;
using Novolis.Logging.Contracts;

namespace Novolis.Logging.Telemetry;

/// <summary>Dedicated <see cref="ILoggerProvider"/> for explicit telemetry events.</summary>
[ProviderAlias("NovolisTelemetry")]
public sealed class TelemetryLoggerProvider : ILoggerProvider
{
    private readonly TelemetryJournal _journal;
    private readonly bool _ownsJournal;
    private int _minimumLevel;
    private int _disposed;

    /// <summary>Creates a provider with an owned NDJSON telemetry journal.</summary>
    public TelemetryLoggerProvider(
        NdjsonTelemetryOptions options,
        LogLevel minimumLevel = LogLevel.Information)
        : this(new TelemetryJournal(options), minimumLevel, ownsJournal: true)
    {
    }

    /// <summary>Creates a provider over an externally owned telemetry journal.</summary>
    public TelemetryLoggerProvider(
        TelemetryJournal journal,
        LogLevel minimumLevel = LogLevel.Information)
        : this(journal, minimumLevel, ownsJournal: false)
    {
    }

    private TelemetryLoggerProvider(
        TelemetryJournal journal,
        LogLevel minimumLevel,
        bool ownsJournal)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _minimumLevel = (int)minimumLevel;
        _ownsJournal = ownsJournal;
    }

    /// <summary>Minimum level accepted by this provider.</summary>
    public LogLevel MinimumLevel
    {
        get => (LogLevel)Volatile.Read(ref _minimumLevel);
        set => Volatile.Write(ref _minimumLevel, (int)value);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        return new TelemetryLogger(this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (_ownsJournal)
            _journal.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class TelemetryLogger(TelemetryLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None
            && logLevel >= provider.MinimumLevel
            && Volatile.Read(ref provider._disposed) == 0;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || state is not TelemetryEventState telemetry)
                return;

            try
            {
                var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
                var scopes = LoggingScopeStack.CurrentMerged;
                if (scopes is not null)
                {
                    foreach (var pair in scopes)
                        properties[pair.Key] = pair.Value;
                }

                if (telemetry.Properties is not null)
                {
                    foreach (var pair in telemetry.Properties)
                        properties[pair.Key] = pair.Value;
                }

                if (exception is not null)
                    properties["exception"] = exception.ToString();

                provider._journal.Record(
                    telemetry.Name,
                    telemetry.Value,
                    telemetry.Unit,
                    properties.Count == 0 ? null : properties);
            }
            catch
            {
                // Telemetry must not break the operation being measured.
            }
        }
    }
}
