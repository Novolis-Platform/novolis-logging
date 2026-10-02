namespace Novolis.Logging.Ndjson;

/// <summary>Structured log record persisted as one NDJSON object.</summary>
public sealed class NdjsonLogRecord
{
    /// <summary>UTC time at which the record was accepted.</summary>
    public DateTimeOffset Time { get; init; }

    /// <summary>Text form of the Microsoft logging level.</summary>
    public string Level { get; init; } = string.Empty;

    /// <summary>Logger category.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Formatted log message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Optional event id.</summary>
    public int? EventId { get; init; }

    /// <summary>Optional event name.</summary>
    public string? EventName { get; init; }

    /// <summary>Optional exception text.</summary>
    public string? Exception { get; init; }

    /// <summary>Structured state and scope values.</summary>
    public Dictionary<string, object?>? State { get; init; }
}
