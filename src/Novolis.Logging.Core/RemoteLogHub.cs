using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Novolis.Logging.Contracts;

namespace Novolis.Logging.Core;

/// <summary>Thread-safe ring buffer and fan-out for <see cref="RemoteLogLine"/> records.</summary>
public sealed class RemoteLogHub
{
    private readonly object _gate = new();
    private readonly RemoteLogLine?[] _ring;
    private int _head;
    private int _count;
    private long _sequence;
    private LogLevel _minLevel;

    public RemoteLogHub(IOptions<RemoteLogOptions>? options = null)
    {
        var o = options?.Value ?? new RemoteLogOptions();
        var capacity = Math.Clamp(o.Capacity, 1, 1_000_000);
        _ring = new RemoteLogLine?[capacity];
        _minLevel = o.MinLevel;
        SessionId = o.SessionId;
        SnapshotTailCount = Math.Clamp(o.SnapshotTailCount, 1, capacity);
    }

    public string? SessionId { get; set; }

    public int Capacity => _ring.Length;

    public int SnapshotTailCount { get; set; }

    public LogLevel MinLevel
    {
        get
        {
            lock (_gate)
                return _minLevel;
        }
        set
        {
            lock (_gate)
                _minLevel = value;
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
                return _count;
        }
    }

    public long Sequence
    {
        get
        {
            lock (_gate)
                return _sequence;
        }
    }

    /// <summary>Raised after a line is accepted into the buffer (any thread).</summary>
    public event Action<RemoteLogLine>? LineAppended;

    public void Append(RemoteLogLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        lock (_gate)
        {
            if ((LogLevel)line.L < _minLevel)
                return;

            if (string.IsNullOrEmpty(line.SessionId) && !string.IsNullOrEmpty(SessionId))
                line.SessionId = SessionId;

            _ring[_head] = line;
            _head = (_head + 1) % _ring.Length;
            if (_count < _ring.Length)
                _count++;
            _sequence++;
        }

        LineAppended?.Invoke(line);
    }

    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_ring);
            _head = 0;
            _count = 0;
            _sequence++;
        }
    }

    /// <summary>Returns up to <paramref name="count"/> most recent lines (oldest first).</summary>
    public IReadOnlyList<RemoteLogLine> Tail(int? count = null)
    {
        lock (_gate)
        {
            var n = Math.Clamp(count ?? SnapshotTailCount, 0, _count);
            if (n == 0)
                return Array.Empty<RemoteLogLine>();

            var result = new RemoteLogLine[n];
            var start = (_head - n + _ring.Length) % _ring.Length;
            for (var i = 0; i < n; i++)
            {
                var line = _ring[(start + i) % _ring.Length]
                    ?? throw new InvalidOperationException("Ring buffer gap.");
                result[i] = Clone(line);
            }

            return result;
        }
    }

    public Dictionary<string, string> BuildStatusLines(int? tailCount = null)
    {
        var lines = Tail(tailCount);
        var status = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["log.count"] = Count.ToString(CultureInfo.InvariantCulture),
            ["log.sequence"] = Sequence.ToString(CultureInfo.InvariantCulture),
            ["log.minLevel"] = MinLevel.ToString(),
            ["log.capacity"] = Capacity.ToString(CultureInfo.InvariantCulture),
            ["log.sessionId"] = SessionId ?? "",
            ["log.tailCount"] = lines.Count.ToString(CultureInfo.InvariantCulture),
        };

        if (lines.Count > 0)
        {
            var ndjson = string.Concat(lines.Select(l =>
                System.Text.Encoding.UTF8.GetString(RemoteLogJson.SerializeLine(l))));
            status["log.tail"] = ndjson.TrimEnd('\n');
            status["log.latest"] = System.Text.Encoding.UTF8.GetString(RemoteLogJson.SerializeLine(lines[^1])).TrimEnd('\n');
        }
        else
        {
            status["log.tail"] = "";
            status["log.latest"] = "";
        }

        return status;
    }

    private static RemoteLogLine Clone(RemoteLogLine line) => new()
    {
        T = line.T,
        L = line.L,
        C = line.C,
        M = line.M,
        X = line.X,
        SessionId = line.SessionId,
        Scope = line.Scope is null ? null : new Dictionary<string, string>(line.Scope, StringComparer.Ordinal),
    };
}
