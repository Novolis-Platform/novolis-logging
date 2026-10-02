using System.Text.Json;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Ndjson;

namespace Novolis.Logging.Diagnostics;

/// <summary>
/// Bounded, synchronous, app-private NDJSON diagnostic journal.
/// This is safe to call while the logging host is starting or failing.
/// </summary>
public sealed class DiagnosticJournal : IDiagnosticJournal, INdjsonLogSink, IDisposable
{
    private readonly object _gate = new();
    private readonly string _applicationName;
    private readonly NdjsonFileLogSink _sink;
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
        _sink = new NdjsonFileLogSink(new NdjsonFileSinkOptions
        {
            DirectoryPath = DirectoryPath,
            FilePrefix = "diagnostics",
            MaximumFileBytes = options.MaximumFileBytes,
            RetainedFileCount = options.RetainedFileCount,
        });
        var startup = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["process.id"] = Environment.ProcessId,
            ["runtime"] = Environment.Version.ToString(),
        };
        if (options.StartupState is { Count: > 0 })
        {
            foreach (var pair in options.StartupState)
                startup[pair.Key] = pair.Value;
        }

        if (!startup.ContainsKey("platform"))
            startup["os"] = Environment.OSVersion.ToString();

        Write(
            LogLevel.Information,
            "Novolis.Diagnostics",
            $"Diagnostic journal started for {_applicationName}.",
            startup,
            exception: null,
            flush: true);
    }

    /// <inheritdoc />
    public string DirectoryPath { get; }

    void INdjsonLogSink.Append(NdjsonLogRecord record, bool flushToDisk)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _sink.Append(record, flushToDisk);
        }
    }

    /// <inheritdoc />
    public void Write(
        LogLevel level,
        string category,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? scope = null,
        bool flush = false)
    {
        Dictionary<string, object?>? state = null;
        if (scope is { Count: > 0 })
        {
            state = new Dictionary<string, object?>(scope.Count, StringComparer.Ordinal);
            foreach (var pair in scope)
                state[pair.Key] = pair.Value;
        }

        Write(level, category, message, state, exception, flush);
    }

    /// <inheritdoc />
    public void Write(
        LogLevel level,
        string category,
        string message,
        IReadOnlyDictionary<string, object?>? state,
        Exception? exception = null,
        bool flush = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(message);
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _sink.Append(
                    new NdjsonLogRecord
                    {
                        Time = DateTimeOffset.UtcNow,
                        Level = LevelName((int)level),
                        Category = category,
                        Message = message,
                        Exception = exception?.ToString(),
                        State = state is { Count: > 0 }
                            ? new Dictionary<string, object?>(state, StringComparer.Ordinal)
                            : null,
                    },
                    flushToDisk: flush || level >= LogLevel.Error);
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
    public IReadOnlyList<string> GetRecentFiles() => _sink.GetRecentFiles();

    /// <inheritdoc />
    public string ReadRecentSummary(int maxLines = 12)
    {
        if (maxLines < 1)
            maxLines = 1;

        var included = new List<string>();
        lock (_gate)
        {
            foreach (var path in GetRecentFiles().Reverse())
            {
                string[] raw;
                try
                {
                    raw = File.ReadAllLines(path);
                }
                catch
                {
                    continue;
                }

                foreach (var rawLine in raw)
                {
                    if (TryFormatLine(rawLine, out var formatted))
                        included.Add(formatted);
                }
            }
        }

        if (included.Count == 0)
            return string.Empty;
        if (included.Count > maxLines)
            included = included.GetRange(included.Count - maxLines, maxLines);
        return string.Join(Environment.NewLine, included);
    }

    private static bool TryFormatLine(string rawLine, out string formatted)
    {
        formatted = string.Empty;
        if (string.IsNullOrWhiteSpace(rawLine))
            return false;

        try
        {
            using var document = JsonDocument.Parse(rawLine);
            var root = document.RootElement;
            var level = ReadLevel(root);
            var category = ReadString(root, "category") ?? ReadString(root, "C") ?? string.Empty;
            if (!IncludeInSummary(category, level))
                return false;

            var message = ReadString(root, "message") ?? ReadString(root, "M") ?? string.Empty;
            var when = ReadTime(root);
            var exception = ReadString(root, "exception") ?? ReadString(root, "X");
            exception = FirstLine(exception);
            var state = FormatState(root);
            formatted = string.IsNullOrWhiteSpace(exception)
                ? $"{when}  {LevelName(level)}  {message}{state}"
                : $"{when}  {LevelName(level)}  {message}{state} {exception}";
            return !string.IsNullOrWhiteSpace(message);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int ReadLevel(JsonElement root)
    {
        if (root.TryGetProperty("level", out var named) && named.ValueKind == JsonValueKind.String)
        {
            return named.GetString() switch
            {
                "Trace" => (int)LogLevel.Trace,
                "Debug" => (int)LogLevel.Debug,
                "Information" => (int)LogLevel.Information,
                "Warning" => (int)LogLevel.Warning,
                "Error" => (int)LogLevel.Error,
                "Critical" => (int)LogLevel.Critical,
                _ => (int)LogLevel.Information,
            };
        }

        return root.TryGetProperty("L", out var levelValue) && levelValue.TryGetInt32(out var parsed)
            ? parsed
            : (int)LogLevel.Information;
    }

    private static string ReadTime(JsonElement root)
    {
        if (root.TryGetProperty("time", out var named) &&
            named.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(
                named.GetString(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return parsed.ToLocalTime().ToString(
                "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        return root.TryGetProperty("T", out var timeValue) && timeValue.TryGetInt64(out var unixMs)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime().ToString(
                "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture)
            : "unknown-time";
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string FormatState(JsonElement root)
    {
        if (!root.TryGetProperty("state", out var state) && !root.TryGetProperty("S", out state))
            return string.Empty;
        if (state.ValueKind != JsonValueKind.Object)
            return string.Empty;

        var parts = new List<string>();
        foreach (var property in state.EnumerateObject())
        {
            if (parts.Count == 8)
                break;
            var text = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty,
            };
            if (text.Length > 80)
                text = text[..80];
            if (text.Length > 0)
                parts.Add($"{property.Name}={text}");
        }

        return parts.Count == 0 ? string.Empty : " " + string.Join(" ", parts);
    }

    private static bool IncludeInSummary(string category, int level) =>
        category.Contains("ReadAloud", StringComparison.Ordinal) ||
        category.StartsWith("Novolis.Diagnostics", StringComparison.Ordinal) ||
        level >= (int)LogLevel.Warning;

    private static string LevelName(int level) => level switch
    {
        (int)LogLevel.Trace => "Trace",
        (int)LogLevel.Debug => "Debug",
        (int)LogLevel.Information => "Information",
        (int)LogLevel.Warning => "Warning",
        (int)LogLevel.Error => "Error",
        (int)LogLevel.Critical => "Critical",
        _ => "Log",
    };

    private static string? FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var line = text.Replace('\r', '\n').Split('\n', 2)[0].Trim();
        return line.Length <= 180 ? line : line[..180];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _sink.Dispose();
        }
    }
}
