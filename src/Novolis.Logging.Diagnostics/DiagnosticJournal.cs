using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Diagnostics;

/// <summary>Options for a bounded, app-private diagnostic journal.</summary>
public sealed class DiagnosticJournalOptions
{
    /// <summary>Directory where journal files are stored.</summary>
    public required string DirectoryPath { get; init; }

    /// <summary>Stable product name written to the journal header.</summary>
    public required string ApplicationName { get; init; }

    /// <summary>Maximum size of one journal file before a new file is started.</summary>
    public long MaximumFileBytes { get; init; } = 1_048_576;

    /// <summary>Maximum number of recent journal files retained.</summary>
    public int RetainedFileCount { get; init; } = 5;
}

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

/// <summary>Service-registration extensions for durable diagnostic logging.</summary>
public static class DiagnosticJournalServiceCollectionExtensions
{
    /// <summary>Registers a pre-created journal and uses it as an <see cref="ILogger"/> provider.</summary>
    public static IServiceCollection AddDiagnosticFileLogging(
        this IServiceCollection services,
        IDiagnosticJournal journal)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(journal);

        services.TryAddSingleton(journal);
        services.TryAddSingleton<IDiagnosticJournal>(journal);
        services.AddLogging(logging => logging.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider>(new DiagnosticJournalLoggerProvider(journal))));
        return services;
    }
}
