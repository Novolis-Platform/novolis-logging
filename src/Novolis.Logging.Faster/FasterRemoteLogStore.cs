using System.Text;
using FASTER.core;
using Microsoft.Extensions.Options;
using Novolis.Logging.Contracts;
using Novolis.Logging.Core;

namespace Novolis.Logging.Faster;

/// <summary>
/// Durable append-only store for <see cref="RemoteLogLine"/> records backed by Microsoft FASTER FasterLog.
/// Complements the in-memory <see cref="RemoteLogHub"/> ring buffer with recoverable disk persistence.
/// </summary>
public sealed class FasterRemoteLogStore : IAsyncDisposable, IDisposable
{
    private readonly object _gate = new();
    private readonly FasterLogSettings _settings;
    private readonly FasterLog _log;
    private readonly CancellationTokenSource? _commitCts;
    private readonly Task? _commitLoop;
    private RemoteLogHub? _hub;
    private bool _disposed;

    public FasterRemoteLogStore(IOptions<FasterRemoteLogOptions> options)
        : this(options?.Value ?? throw new ArgumentNullException(nameof(options)))
    {
    }

    public FasterRemoteLogStore(FasterRemoteLogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Directory))
            throw new ArgumentException("FasterRemoteLogOptions.Directory is required.", nameof(options));

        Directory = Path.GetFullPath(options.Directory);
        System.IO.Directory.CreateDirectory(Directory);

        _settings = new FasterLogSettings(Directory);
        if (options.PageSizeBits is int pageBits)
            _settings.PageSizeBits = pageBits;
        if (options.MemorySizeBits is int memoryBits)
            _settings.MemorySizeBits = memoryBits;
        if (options.SegmentSizeBits is int segmentBits)
            _settings.SegmentSizeBits = segmentBits;

        _log = new FasterLog(_settings);
        AutoCommit = options.AutoCommit;
        CommitInterval = options.CommitInterval <= TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(50)
            : options.CommitInterval;

        if (AutoCommit)
        {
            _commitCts = new CancellationTokenSource();
            _commitLoop = Task.Run(() => CommitLoopAsync(_commitCts.Token));
        }
    }

    /// <summary>Absolute path of the FasterLog directory.</summary>
    public string Directory { get; }

    public bool AutoCommit { get; }

    public TimeSpan CommitInterval { get; }

    /// <summary>Logical address through which FasterLog has committed.</summary>
    public long CommittedUntilAddress
    {
        get
        {
            ThrowIfDisposed();
            return _log.CommittedUntilAddress;
        }
    }

    /// <summary>Subscribe to hub fan-out so every accepted line is enqueued to FasterLog.</summary>
    public void Attach(RemoteLogHub hub)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ThrowIfDisposed();

        lock (_gate)
        {
            if (ReferenceEquals(_hub, hub))
                return;
            DetachCore();
            _hub = hub;
            hub.LineAppended += OnLineAppended;
        }
    }

    /// <summary>Stop receiving hub fan-out (store remains open for direct <see cref="Append"/>).</summary>
    public void Detach()
    {
        lock (_gate)
            DetachCore();
    }

    /// <summary>Enqueue a line into FasterLog memory (call <see cref="CommitAsync"/> for durability).</summary>
    public long Append(RemoteLogLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        ThrowIfDisposed();
        var bytes = RemoteLogJson.SerializeLine(line);
        return _log.Enqueue(bytes);
    }

    /// <summary>Force a recoverable commit of enqueued entries.</summary>
    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return _log.CommitAsync(token: cancellationToken);
    }

    /// <summary>Spin-wait commit (prefer <see cref="CommitAsync"/> under load).</summary>
    public void Commit(bool spinWait = true)
    {
        ThrowIfDisposed();
        _log.Commit(spinWait);
    }

    /// <summary>
    /// Read committed entries oldest-first. When <paramref name="maxCount"/> is set, returns the
    /// most recent committed lines only.
    /// </summary>
    public async Task<IReadOnlyList<RemoteLogLine>> ReadCommittedAsync(
        int? maxCount = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var begin = _log.BeginAddress;
        var end = _log.CommittedUntilAddress;
        if (end <= begin)
            return Array.Empty<RemoteLogLine>();

        var lines = new List<RemoteLogLine>();
        using var iter = _log.Scan(begin, end);
        await foreach (var (entry, length, _, _) in iter.GetAsyncEnumerable(cancellationToken).ConfigureAwait(false))
        {
            var text = Encoding.UTF8.GetString(entry.AsSpan(0, length));
            var line = RemoteLogJson.TryDeserializeLine(text);
            if (line is not null)
                lines.Add(line);
        }

        if (maxCount is int n && n >= 0 && lines.Count > n)
            return lines.GetRange(lines.Count - n, n);

        return lines;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Detach();
        StopCommitLoop();
        try
        {
            _log.Commit(spinWait: true);
        }
        catch
        {
            // Best-effort final commit on dispose.
        }

        _log.Dispose();
        _settings.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        Detach();
        await StopCommitLoopAsync().ConfigureAwait(false);
        try
        {
            await _log.CommitAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort final commit on dispose.
        }

        _log.Dispose();
        _settings.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnLineAppended(RemoteLogLine line)
    {
        try
        {
            Append(line);
        }
        catch
        {
            // Hub fan-out must not throw into producers; durable sink failures are non-fatal here.
        }
    }

    private void DetachCore()
    {
        if (_hub is null)
            return;
        _hub.LineAppended -= OnLineAppended;
        _hub = null;
    }

    private async Task CommitLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(CommitInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await _log.CommitAsync(token: cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Keep the loop alive across transient I/O errors.
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void StopCommitLoop()
    {
        if (_commitCts is null)
            return;
        _commitCts.Cancel();
        try
        {
            _commitLoop?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _commitCts.Dispose();
    }

    private async Task StopCommitLoopAsync()
    {
        if (_commitCts is null)
            return;
        await _commitCts.CancelAsync().ConfigureAwait(false);
        if (_commitLoop is not null)
        {
            try
            {
                await _commitLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _commitCts.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
