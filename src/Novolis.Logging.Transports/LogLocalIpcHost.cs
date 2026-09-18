using System.Text;
using System.Text.Json;
using Novolis.Logging.Contracts;
using Novolis.Logging.Core;
using Novolis.Transports.LocalIpc;

namespace Novolis.Logging.Transports;

/// <summary>LocalIpc host for <c>log.ingest</c>, <c>log.tail</c>, and <c>log.subscribe</c> (+ <c>log.line</c> events).</summary>
public sealed class LogLocalIpcHost : IAsyncDisposable
{
    private readonly RemoteLogHub _hub;
    private readonly LocalIpcEndpoint _endpoint;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenTask;
    private readonly object _subscribersGate = new();
    private readonly HashSet<ILocalIpcConnection> _subscribers = new();
    private readonly string _markerPath;
    private ILocalIpcListener? _listener;
    private bool _disposed;

    private LogLocalIpcHost(RemoteLogHub hub, LocalIpcEndpoint endpoint, string markerPrefix)
    {
        _hub = hub;
        _endpoint = endpoint;
        _markerPath = Path.Combine(Path.GetTempPath(), $"{markerPrefix}.ipc");
        _hub.LineAppended += OnLine;
        _listenTask = Task.Run(() => ListenAsync(_cts.Token));
    }

    public LocalIpcEndpoint Endpoint => _endpoint;

    public static LogLocalIpcHost Attach(
        RemoteLogHub hub,
        string address = "novolis-logging",
        string markerPrefix = "novolis-logging-transport")
    {
        ArgumentNullException.ThrowIfNull(hub);
        return new LogLocalIpcHost(hub, new LocalIpcEndpoint(address), markerPrefix);
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            await File.WriteAllTextAsync(
                    _markerPath,
                    $"{Environment.ProcessId}\n{_endpoint.Kind}\n{_endpoint.Address}\n",
                    cancellationToken)
                .ConfigureAwait(false);

            _listener = LocalIpcTransport.CreateListener(_endpoint);
            while (!cancellationToken.IsCancellationRequested)
            {
                var connection = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleConnectionAsync(connection, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            try
            {
                await File.WriteAllTextAsync(_markerPath + ".error", ex.ToString(), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task HandleConnectionAsync(ILocalIpcConnection connection, CancellationToken cancellationToken)
    {
        await using (connection)
        {
            try
            {
                await foreach (var frame in connection.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!string.Equals(frame.Kind, "request", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        await DispatchAsync(connection, frame, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        var fault = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
                        await connection.SendAsync(
                                new LocalIpcFrame(frame.Sequence, "fault", frame.Name, fault),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                lock (_subscribersGate)
                    _subscribers.Remove(connection);
            }
        }
    }

    private async Task DispatchAsync(
        ILocalIpcConnection connection,
        LocalIpcFrame frame,
        CancellationToken cancellationToken)
    {
        if (string.Equals(frame.Name, LogMethodNames.Ingest, StringComparison.OrdinalIgnoreCase))
        {
            var accepted = IngestPayload(frame.Payload);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true, accepted }));
            await connection.SendAsync(
                    new LocalIpcFrame(frame.Sequence, "response", frame.Name, bytes),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.Equals(frame.Name, LogMethodNames.Tail, StringComparison.OrdinalIgnoreCase))
        {
            var count = ParseCount(frame.Payload) ?? 50;
            var sb = new StringBuilder();
            foreach (var line in _hub.Tail(count))
                sb.Append(Encoding.UTF8.GetString(RemoteLogJson.SerializeLine(line)));
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            await connection.SendAsync(
                    new LocalIpcFrame(frame.Sequence, "response", frame.Name, bytes),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.Equals(frame.Name, LogMethodNames.Subscribe, StringComparison.OrdinalIgnoreCase))
        {
            lock (_subscribersGate)
                _subscribers.Add(connection);
            var bytes = Encoding.UTF8.GetBytes("""{"ok":true}""");
            await connection.SendAsync(
                    new LocalIpcFrame(frame.Sequence, "response", frame.Name, bytes),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var unknown = Encoding.UTF8.GetBytes("""{"ok":false,"error":"unknown method"}""");
        await connection.SendAsync(
                new LocalIpcFrame(frame.Sequence, "fault", frame.Name, unknown),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private int IngestPayload(byte[] payload)
    {
        var text = Encoding.UTF8.GetString(payload);
        var accepted = 0;
        foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = RemoteLogJson.TryDeserializeLine(raw);
            if (line is null)
                continue;
            _hub.Append(line);
            accepted++;
        }

        return accepted;
    }

    private static int? ParseCount(byte[] payload)
    {
        if (payload.Length == 0)
            return null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("count", out var c) && c.TryGetInt32(out var n))
                return n;
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private void OnLine(RemoteLogLine line)
    {
        ILocalIpcConnection[] subs;
        lock (_subscribersGate)
            subs = _subscribers.ToArray();

        var payload = RemoteLogJson.SerializeLine(line);
        foreach (var sub in subs)
        {
            try
            {
                sub.SendAsync(new LocalIpcFrame(0, "event", LogMethodNames.Line, payload)).AsTask()
                    .GetAwaiter().GetResult();
            }
            catch
            {
                lock (_subscribersGate)
                    _subscribers.Remove(sub);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        _hub.LineAppended -= OnLine;
        _cts.Cancel();
        if (_listener is not null)
        {
            try { await _listener.DisposeAsync().ConfigureAwait(false); }
            catch { /* ignore */ }
        }

        try { await _listenTask.ConfigureAwait(false); }
        catch { /* ignore */ }
        _cts.Dispose();
        try { File.Delete(_markerPath); }
        catch { /* ignore */ }
    }
}
