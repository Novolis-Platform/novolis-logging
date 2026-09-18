using System.Net;
using System.Text;
using Novolis.Logging.Contracts;
using Novolis.Logging.Core;

namespace Novolis.Logging.Transports;

/// <summary>
/// HTTP host for remote logs: <c>POST /log/ingest</c> (NDJSON), <c>GET /log/tail</c>, <c>GET /log/events</c> (SSE).
/// </summary>
public sealed class LogHttpHost : IAsyncDisposable
{
    private readonly RemoteLogHub _hub;
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly object _sseGate = new();
    private readonly HashSet<HttpListenerResponse> _sseClients = new();
    private readonly string _markerPath;
    private bool _disposed;

    private LogHttpHost(RemoteLogHub hub, int port, string markerPrefix)
    {
        _hub = hub;
        Port = port;
        BaseUrl = $"http://127.0.0.1:{port}/";
        _markerPath = Path.Combine(Path.GetTempPath(), $"{markerPrefix}.http");
        _listener = new HttpListener();
        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();
        WriteMarker();
        _hub.LineAppended += OnLine;
        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public int Port { get; }

    public string BaseUrl { get; }

    public static LogHttpHost Attach(RemoteLogHub hub, int port = 18869, string markerPrefix = "novolis-logging-transport")
    {
        ArgumentNullException.ThrowIfNull(hub);
        return new LogHttpHost(hub, port, markerPrefix);
    }

    private void WriteMarker()
    {
        try
        {
            File.WriteAllText(_markerPath, $"{Environment.ProcessId}\n{Port}\n{BaseUrl}\n");
        }
        catch
        {
            // ignore marker failures
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleAsync(context, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? "";
        try
        {
            WriteCors(context.Response);

            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            if (path.Equals("/log/ingest", StringComparison.OrdinalIgnoreCase)
                && context.Request.HttpMethod == "POST")
            {
                await HandleIngestAsync(context, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/log/tail", StringComparison.OrdinalIgnoreCase)
                && context.Request.HttpMethod == "GET")
            {
                await HandleTailAsync(context).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/log/events", StringComparison.OrdinalIgnoreCase)
                && context.Request.HttpMethod == "GET")
            {
                await HandleSseAsync(context, cancellationToken).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = 404;
            context.Response.Close();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task HandleIngestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var accepted = 0;
        foreach (var raw in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = RemoteLogJson.TryDeserializeLine(raw);
            if (line is null)
                continue;
            _hub.Append(line);
            accepted++;
        }

        var bytes = Encoding.UTF8.GetBytes($"{{\"ok\":true,\"accepted\":{accepted}}}\n");
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task HandleTailAsync(HttpListenerContext context)
    {
        var count = 50;
        var q = context.Request.QueryString["count"];
        if (!string.IsNullOrEmpty(q) && int.TryParse(q, out var parsed))
            count = parsed;

        var sb = new StringBuilder();
        foreach (var line in _hub.Tail(count))
            sb.Append(Encoding.UTF8.GetString(RemoteLogJson.SerializeLine(line)));

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/x-ndjson";
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task HandleSseAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.SendChunked = true;

        lock (_sseGate)
            _sseClients.Add(context.Response);

        try
        {
            foreach (var line in _hub.Tail())
                await WriteSseAsync(context.Response, line, cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
                await Task.Delay(15_000, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_sseGate)
                _sseClients.Remove(context.Response);
            try { context.Response.Close(); }
            catch { /* ignore */ }
        }
    }

    private void OnLine(RemoteLogLine line)
    {
        HttpListenerResponse[] clients;
        lock (_sseGate)
            clients = _sseClients.ToArray();

        foreach (var client in clients)
        {
            try
            {
                WriteSseAsync(client, line, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }
            catch
            {
                lock (_sseGate)
                    _sseClients.Remove(client);
                try { client.Close(); }
                catch { /* ignore */ }
            }
        }
    }

    private static async ValueTask WriteSseAsync(
        HttpListenerResponse response,
        RemoteLogLine line,
        CancellationToken cancellationToken)
    {
        var json = Encoding.UTF8.GetString(RemoteLogJson.SerializeLine(line)).TrimEnd('\n');
        var payload = Encoding.UTF8.GetBytes($"event: log\ndata: {json}\n\n");
        await response.OutputStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await response.OutputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void WriteCors(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        _hub.LineAppended -= OnLine;
        _cts.Cancel();
        try { _listener.Stop(); }
        catch { /* ignore */ }
        _listener.Close();
        try { await _loop.ConfigureAwait(false); }
        catch { /* ignore */ }
        _cts.Dispose();
        try { File.Delete(_markerPath); }
        catch { /* ignore */ }
    }
}
