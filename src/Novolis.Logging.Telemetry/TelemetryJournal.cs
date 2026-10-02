using Novolis.Storage.Ndjson;

namespace Novolis.Logging.Telemetry;

/// <summary>Typed local telemetry journal backed by the dedicated NDJSON store.</summary>
public sealed class TelemetryJournal : IDisposable
{
    private readonly NdjsonStore _store;
    private bool _disposed;

    /// <summary>Creates a journal using the supplied telemetry options.</summary>
    public TelemetryJournal(NdjsonTelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        FilePath = Path.GetFullPath(options.FilePath);
        _store = new NdjsonStore(
            FilePath,
            new NdjsonStoreOptions(options.JsonSerializerOptions));
        FlushToDisk = options.FlushToDisk;
    }

    /// <summary>Absolute physical path of the telemetry journal.</summary>
    public string FilePath { get; }

    /// <summary>Whether each record requests a durable flush.</summary>
    public bool FlushToDisk { get; }

    /// <summary>Records one named telemetry event.</summary>
    public void Record(
        string name,
        double? value = null,
        string? unit = null,
        IReadOnlyDictionary<string, object?>? properties = null,
        bool? flushToDisk = null)
    {
        ThrowIfDisposed();
        _store.Append(
            CreateEvent(name, value, unit, properties),
            flushToDisk ?? FlushToDisk);
    }

    /// <summary>Records one named telemetry event asynchronously.</summary>
    public ValueTask RecordAsync(
        string name,
        double? value = null,
        string? unit = null,
        IReadOnlyDictionary<string, object?>? properties = null,
        bool? flushToDisk = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _store.AppendAsync(
            CreateEvent(name, value, unit, properties),
            cancellationToken,
            flushToDisk ?? FlushToDisk);
    }

    /// <summary>Reads valid telemetry events in physical order.</summary>
    public IAsyncEnumerable<TelemetryEvent> ReadAsync(
        CancellationToken cancellationToken = default) =>
        _store.ReadAsync<TelemetryEvent>(cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _store.Dispose();
        GC.SuppressFinalize(this);
    }

    private static TelemetryEvent CreateEvent(
        string name,
        double? value,
        string? unit,
        IReadOnlyDictionary<string, object?>? properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (value is { } numeric && !double.IsFinite(numeric))
            throw new ArgumentOutOfRangeException(nameof(value), "Telemetry values must be finite.");

        return new TelemetryEvent
        {
            Time = DateTimeOffset.UtcNow,
            Name = name.Trim(),
            Value = value,
            Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(),
            Properties = properties is { Count: > 0 }
                ? new Dictionary<string, object?>(properties, StringComparer.Ordinal)
                : null,
        };
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
