using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Ndjson;

/// <summary>Dependency-injection registration for the NDJSON logging provider.</summary>
public static class NdjsonLoggingExtensions
{
    /// <summary>Adds a rotating NDJSON file provider to the logging builder.</summary>
    public static ILoggingBuilder AddNdjsonLogging(
        this ILoggingBuilder logging,
        Action<NdjsonFileSinkOptions> configure,
        LogLevel minimumLevel = LogLevel.Trace)
    {
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new NdjsonFileSinkOptions();
        configure(options);
        options.Validate();
        logging.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider>(
                new NdjsonLoggerProvider(options, minimumLevel)));
        return logging;
    }

    /// <summary>Adds an NDJSON provider over an application-owned sink.</summary>
    public static ILoggingBuilder AddNdjsonLogging(
        this ILoggingBuilder logging,
        INdjsonLogSink sink,
        LogLevel minimumLevel = LogLevel.Trace)
    {
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(sink);
        logging.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider>(
                new NdjsonLoggerProvider(sink, minimumLevel)));
        return logging;
    }
}
