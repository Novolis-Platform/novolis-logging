using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Contracts;

namespace Novolis.Logging.Telemetry;

/// <summary>Dependency-injection registration for explicit NDJSON telemetry.</summary>
public static class TelemetryLoggingExtensions
{
    /// <summary>Adds the opt-in telemetry provider to the logging builder.</summary>
    public static ILoggingBuilder AddNdjsonTelemetry(
        this ILoggingBuilder logging,
        Action<NdjsonTelemetryOptions> configure,
        LogLevel minimumLevel = LogLevel.Information)
    {
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new NdjsonTelemetryOptions();
        configure(options);
        options.Validate();
        logging.AddLoggingScopeStackBridge();
        logging.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider>(
                new TelemetryLoggerProvider(options, minimumLevel)));
        return logging;
    }
}
