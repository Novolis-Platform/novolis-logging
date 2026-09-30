using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Diagnostics;

/// <summary>Writes <see cref="ILogger"/> events into an <see cref="IDiagnosticJournal"/>.</summary>
[ProviderAlias("NovolisDiagnosticFile")]
public sealed class DiagnosticJournalLoggerProvider(IDiagnosticJournal journal) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new JournalLogger(journal, categoryName);

    public void Dispose()
    {
    }

    private sealed class JournalLogger(IDiagnosticJournal journal, string category) : ILogger
    {
        private static readonly AsyncLocal<Stack<Dictionary<string, object?>>?> Scopes = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            var incoming = DiagnosticStateReader.Read(state);
            if (incoming is null || incoming.Count == 0)
                return EmptyScope.Instance;

            var stack = Scopes.Value ?? new Stack<Dictionary<string, object?>>();
            Scopes.Value = stack;
            var parent = stack.Count > 0 ? stack.Peek() : null;
            stack.Push(DiagnosticStateReader.Merge(parent, incoming));
            return new PopScope(stack);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

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
            string message;
            IReadOnlyDictionary<string, object?>? properties;
            if (state is DiagnosticEvent diagnostic)
            {
                message = diagnostic.Message;
                properties = diagnostic.Properties;
            }
            else
            {
                message = formatter(state, exception);
                properties = DiagnosticStateReader.Read(state);
            }

            var stack = Scopes.Value;
            var scope = stack is { Count: > 0 } ? stack.Peek() : null;
            var merged = DiagnosticStateReader.Merge(scope, properties);
            journal.Write(
                logLevel,
                category,
                message,
                merged.Count == 0 ? null : merged,
                exception,
                flush: logLevel >= LogLevel.Error);
        }

        private sealed class PopScope(Stack<Dictionary<string, object?>> stack) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                if (stack.Count > 0)
                    stack.Pop();
                if (stack.Count == 0)
                    Scopes.Value = null;
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            internal static readonly EmptyScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
