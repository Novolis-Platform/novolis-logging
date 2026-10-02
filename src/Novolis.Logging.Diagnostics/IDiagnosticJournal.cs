using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Ndjson;

namespace Novolis.Logging.Diagnostics;

/// <summary>App-private diagnostic files available for support export.</summary>
public interface IDiagnosticJournal : INdjsonLogSink
{
    /// <summary>Directory containing the bounded journal files.</summary>
    string DirectoryPath { get; }

    /// <summary>Writes an application log event without exposing user content by default.</summary>
    void Write(
        LogLevel level,
        string category,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? scope = null,
        bool flush = false);

    /// <summary>Writes an event whose named state keeps numbers as numbers.</summary>
    void Write(
        LogLevel level,
        string category,
        string message,
        IReadOnlyDictionary<string, object?>? state,
        Exception? exception = null,
        bool flush = false)
    {
        IReadOnlyDictionary<string, string>? scope = null;
        if (state is { Count: > 0 })
        {
            var copy = new Dictionary<string, string>(state.Count, StringComparer.Ordinal);
            foreach (var pair in state)
                copy[pair.Key] = pair.Value?.ToString() ?? string.Empty;
            scope = copy;
        }

        Write(level, category, message, exception, scope, flush);
    }

    /// <summary>Writes an exception without requiring the logging host to be available.</summary>
    void WriteException(string source, Exception exception);

    /// <summary>Returns retained journal files, newest first.</summary>
    IReadOnlyList<string> GetRecentFiles();

    /// <summary>
    /// Returns recent speech and failure lines as readable text, oldest first.
    /// Hosting noise is omitted so the readout is the activity that matters.
    /// </summary>
    string ReadRecentSummary(int maxLines = 12);
}
