using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Contracts;

/// <summary>
/// Silent provider so <see cref="ILogger.BeginScope"/> is applied exactly once per logical scope
/// (the host aggregates scopes across all <see cref="ILoggerProvider"/> instances).
/// </summary>
public sealed class LoggingScopeBridgeLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => Instance;

    public void Dispose()
    {
    }

    private static readonly BridgeLogger Instance = new();

    private sealed class BridgeLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            LoggingScopeStack.Push(state);

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
