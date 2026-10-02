using System.Text.Json;

namespace Novolis.Logging.Telemetry;

/// <summary>Options for local NDJSON telemetry persistence.</summary>
public sealed class NdjsonTelemetryOptions
{
    /// <summary>Absolute or host-relative path of the telemetry file.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Flushes each telemetry event to the storage device when enabled.</summary>
    public bool FlushToDisk { get; set; }

    /// <summary>Serializer options used by the telemetry store.</summary>
    public JsonSerializerOptions JsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
            throw new ArgumentException("A telemetry file path is required.", nameof(FilePath));
    }
}
