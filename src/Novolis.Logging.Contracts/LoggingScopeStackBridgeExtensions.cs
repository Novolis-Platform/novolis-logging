using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Contracts;

public static class LoggingScopeStackBridgeExtensions
{
    public static ILoggingBuilder AddLoggingScopeStackBridge(this ILoggingBuilder logging)
    {
        logging.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, LoggingScopeBridgeLoggerProvider>());
        return logging;
    }
}
