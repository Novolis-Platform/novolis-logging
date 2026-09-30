using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Diagnostics;

/// <summary>App-private diagnostic files available for support export.</summary>
public interface IDiagnosticJournal
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

    /// <summary>Writes an exception without requiring the logging host to be available.</summary>
    void WriteException(string source, Exception exception);

    /// <summary>Returns retained journal files, newest first.</summary>
    IReadOnlyList<string> GetRecentFiles();
}
