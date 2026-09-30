using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Diagnostics;

/// <summary>
/// Bounded, synchronous, app-private NDJSON diagnostic journal.
/// This is safe to call while the logging host is starting or failing.
/// </summary>
public sealed class DiagnosticJournal : IDiagnosticJournal, IDisposable
{
    private readonly object _gate = new();
    private readonly string _applicationName;
    private readonly long _maximumFileBytes;
    private readonly int _retainedFileCount;
    private string _currentPath;
    private int _rollSequence;
    private bool _disposed;

    public DiagnosticJournal(DiagnosticJournalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApplicationName);
        if (options.MaximumFileBytes < 4_096)
            throw new ArgumentOutOfRangeException(nameof(options), "MaximumFileBytes must be at least 4096.");
        if (options.RetainedFileCount < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "RetainedFileCount must be at least one.");

        DirectoryPath = Path.GetFullPath(options.DirectoryPath);
        _applicationName = options.ApplicationName.Trim();
        _maximumFileBytes = options.MaximumFileBytes;
        _retainedFileCount = options.RetainedFileCount;
        Directory.CreateDirectory(DirectoryPath);
        _currentPath = CreatePath();
        Write(
            LogLevel.Information,
            "Novolis.Diagnostics",
            $"Diagnostic journal started for {_applicationName}.",
            scope: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["process.id"] = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["runtime"] = Environment.Version.ToString(),
                ["os"] = Environment.OSVersion.ToString(),
            },
            flush: true);
    }

    /// <inheritdoc />
    public string DirectoryPath { get; }

    /// <inheritdoc />
    public void Write(
        LogLevel level,
        string category,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? scope = null,
        bool flush = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(message);
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new DiagnosticJournalLine
                {
                    T = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    L = (int)level,
                    C = category,
                    M = message,
                    X = exception?.ToString(),
                    S = scope is { Count: > 0 }
                        ? new Dictionary<string, string>(scope, StringComparer.Ordinal)
                        : null,
                }).Append((byte)'\n').ToArray();
                if (File.Exists(_currentPath) &&
                    new FileInfo(_currentPath).Length + bytes.Length > _maximumFileBytes)
                    _currentPath = CreatePath();

                using var stream = new FileStream(
                    _currentPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4_096,
                    FileOptions.None);
                stream.Write(bytes);
                if (flush)
                    stream.Flush(flushToDisk: true);
                TrimRetainedFiles();
            }
        }
        catch
        {
            // Diagnostic recording must never replace the original application failure.
        }
    }

    /// <inheritdoc />
    public void WriteException(string source, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(exception);
        Write(LogLevel.Critical, $"Novolis.Diagnostics.{source}", "Unhandled exception.", exception, flush: true);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetRecentFiles() =>
        Directory.Exists(DirectoryPath)
            ? Directory.EnumerateFiles(DirectoryPath, "diagnostics-*.ndjson")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(_retainedFileCount)
                .ToArray()
            : Array.Empty<string>();

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
            _disposed = true;
    }

    private string CreatePath()
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);
        return Path.Combine(
            DirectoryPath,
            $"diagnostics-{stamp}-{Environment.ProcessId}-{Interlocked.Increment(ref _rollSequence):D2}.ndjson");
    }

    private void TrimRetainedFiles()
    {
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "diagnostics-*.ndjson")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(_retainedFileCount))
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // A user may be sharing a file at this moment; retain it until next write.
            }
        }
    }

    private sealed class DiagnosticJournalLine
    {
        public long T { get; init; }
        public int L { get; init; }
        public required string C { get; init; }
        public required string M { get; init; }
        public string? X { get; init; }
        public Dictionary<string, string>? S { get; init; }
    }
}
