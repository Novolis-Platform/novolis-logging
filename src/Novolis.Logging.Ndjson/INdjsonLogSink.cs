namespace Novolis.Logging.Ndjson;

/// <summary>Receives structured records from <see cref="NdjsonLoggerProvider"/>.</summary>
public interface INdjsonLogSink
{
    /// <summary>Appends one record, optionally requesting durable flush.</summary>
    void Append(NdjsonLogRecord record, bool flushToDisk = false);
}
