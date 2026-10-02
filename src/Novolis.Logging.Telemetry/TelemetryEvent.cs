namespace Novolis.Logging.Telemetry;

/// <summary>One opt-in application telemetry measurement.</summary>
public sealed class TelemetryEvent
{
    /// <summary>UTC time at which the event was recorded.</summary>
    public DateTimeOffset Time { get; init; }

    /// <summary>Stable event name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional numeric measurement.</summary>
    public double? Value { get; init; }

    /// <summary>Optional unit for <see cref="Value"/>.</summary>
    public string? Unit { get; init; }

    /// <summary>Named dimensions and diagnostic values.</summary>
    public Dictionary<string, object?>? Properties { get; init; }
}
