using System.Text.Json;
using Novolis.Storage.Ndjson;

namespace Novolis.Logging.Ndjson;

/// <summary>Rotating local-file sink for structured NDJSON log records.</summary>
public sealed class NdjsonFileLogSink : INdjsonLogSink, IDisposable
{
    private readonly object _gate = new();
    private readonly NdjsonFileSinkOptions _options;
    private NdjsonStore _store;
    private string _currentPath;
    private int _rollSequence;
    private bool _disposed;

    /// <summary>Creates a rotating sink using <paramref name="options"/>.</summary>
    public NdjsonFileLogSink(NdjsonFileSinkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        DirectoryPath = Path.GetFullPath(options.DirectoryPath);
        Directory.CreateDirectory(DirectoryPath);
        _currentPath = CreatePath();
        _store = CreateStore(_currentPath);
    }

    /// <summary>Directory containing the retained files.</summary>
    public string DirectoryPath { get; }

    /// <summary>Returns retained files from newest to oldest.</summary>
    public IReadOnlyList<string> GetRecentFiles() =>
        Directory.Exists(DirectoryPath)
            ? Directory.EnumerateFiles(DirectoryPath, $"{_options.FilePrefix}-*.ndjson")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(_options.RetainedFileCount)
                .ToArray()
            : Array.Empty<string>();

    /// <inheritdoc />
    public void Append(NdjsonLogRecord record, bool flushToDisk = false)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(record, _options.JsonSerializerOptions);
            var current = new FileInfo(_currentPath);
            if (current.Exists && current.Length + bytes.Length + 1 > _options.MaximumFileBytes)
            {
                _store.Dispose();
                _currentPath = CreatePath();
                _store = CreateStore(_currentPath);
            }

            _store.AppendJson(bytes, flushToDisk);
            TrimRetainedFiles();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _store.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private NdjsonStore CreateStore(string path) =>
        new(
            path,
            new NdjsonStoreOptions(_options.JsonSerializerOptions));

    private string CreatePath()
    {
        var stamp = DateTime.UtcNow.ToString(
            "yyyyMMdd-HHmmss-fff",
            System.Globalization.CultureInfo.InvariantCulture);
        return Path.Combine(
            DirectoryPath,
            $"{_options.FilePrefix}-{stamp}-{Environment.ProcessId}-{++_rollSequence:D2}.ndjson");
    }

    private void TrimRetainedFiles()
    {
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, $"{_options.FilePrefix}-*.ndjson")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(_options.RetainedFileCount))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
