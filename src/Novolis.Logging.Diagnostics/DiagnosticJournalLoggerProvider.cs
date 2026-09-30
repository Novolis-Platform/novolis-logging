using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

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
            journal.Write(
                logLevel,
                category,
                formatter(state, exception),
                exception,
                flush: logLevel >= LogLevel.Error);
        }
    }
}
