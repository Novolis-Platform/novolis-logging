using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Contracts;

namespace Novolis.Logging.Core;

public static class RemoteLogServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="RemoteLogHub"/>, scope-stack bridge, and <see cref="ILoggerProvider"/> sink.
    /// </summary>
    public static IServiceCollection AddRemoteLogging(
        this IServiceCollection services,
        Action<RemoteLogOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<RemoteLogOptions>();

        services.TryAddSingleton<RemoteLogHub>();
        services.AddLogging(b =>
        {
            b.AddLoggingScopeStackBridge();
            b.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, RemoteLogLoggerProvider>());
        });

        return services;
    }

    /// <summary>Adds the remote-log <see cref="ILoggerProvider"/> when the hub is already registered.</summary>
    public static ILoggingBuilder AddRemoteLogHub(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);
        logging.AddLoggingScopeStackBridge();
        logging.Services.TryAddSingleton<RemoteLogHub>();
        logging.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, RemoteLogLoggerProvider>());
        return logging;
    }
}
