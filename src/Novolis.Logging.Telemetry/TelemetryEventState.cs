namespace Novolis.Logging.Telemetry;

/// <summary>Marker state consumed by <see cref="TelemetryLoggerProvider"/>.</summary>
public sealed class TelemetryEventState
{
    /// <summary>Creates one telemetry marker state.</summary>
    public TelemetryEventState(
        string name,
        double? value,
        string? unit,
        IReadOnlyDictionary<string, object?>? properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Value = value;
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        Properties = properties;
    }

    /// <summary>Stable event name.</summary>
    public string Name { get; }

    /// <summary>Optional numeric value.</summary>
    public double? Value { get; }

    /// <summary>Optional numeric unit.</summary>
    public string? Unit { get; }

    /// <summary>Named event properties.</summary>
    public IReadOnlyDictionary<string, object?>? Properties { get; }
}
