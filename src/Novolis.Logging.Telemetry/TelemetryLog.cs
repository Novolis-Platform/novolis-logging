using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Telemetry;

/// <summary>Explicit telemetry helpers that remain distinct from ordinary application logs.</summary>
public static class TelemetryLog
{
    /// <summary>Emits an opt-in telemetry marker to registered telemetry providers.</summary>
    public static void Record(
        ILogger? logger,
        string name,
        double? value = null,
        string? unit = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (logger is null)
            return;

        var state = new TelemetryEventState(name, value, unit, properties);
        logger.Log(
            LogLevel.Information,
            new EventId(0, state.Name),
            state,
            exception: null,
            static (current, _) => current.Name);
    }
}
