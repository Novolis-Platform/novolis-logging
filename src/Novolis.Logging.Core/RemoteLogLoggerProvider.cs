using Microsoft.Extensions.Logging;
using Novolis.Logging.Contracts;

namespace Novolis.Logging.Core;

/// <summary>Writes <see cref="ILogger"/> records into a <see cref="RemoteLogHub"/> as <see cref="RemoteLogLine"/>.</summary>
[ProviderAlias("NovolisRemoteLog")]
public sealed class RemoteLogLoggerProvider : ILoggerProvider
{
    private readonly RemoteLogHub _hub;

    public RemoteLogLoggerProvider(RemoteLogHub hub) =>
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));

    public ILogger CreateLogger(string categoryName) => new HubLogger(_hub, categoryName);

    public void Dispose()
    {
    }

    private sealed class HubLogger(RemoteLogHub hub, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None && logLevel >= hub.MinLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            ArgumentNullException.ThrowIfNull(formatter);

            var scope = LoggingScopeStack.CurrentMerged;
            Dictionary<string, string>? scopeCopy = null;
            if (scope is { Count: > 0 })
                scopeCopy = new Dictionary<string, string>(scope, StringComparer.Ordinal);

            hub.Append(new RemoteLogLine
            {
                T = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                L = (int)logLevel,
                C = category,
                M = formatter(state, exception),
                X = exception?.ToString(),
                SessionId = hub.SessionId,
                Scope = scopeCopy,
            });
        }
    }
}
