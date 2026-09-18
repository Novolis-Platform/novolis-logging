using Novolis.Logging.Core;

namespace Novolis.Logging.Transports;

/// <summary>Attaches HTTP and/or LocalIpc logging transports to a shared <see cref="RemoteLogHub"/>.</summary>
public sealed class LogTransports : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _hosts = new();

    private LogTransports()
    {
    }

    public LogHttpHost? Http { get; private set; }

    public LogLocalIpcHost? Ipc { get; private set; }

    public string? HttpBaseUrl => Http?.BaseUrl;

    /// <summary>Attach transports per <paramref name="options"/>; bind failures are swallowed per host.</summary>
    public static LogTransports? Attach(RemoteLogHub hub, LogTransportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(hub);
        options ??= new LogTransportOptions();

        var attached = new LogTransports();
        var any = false;

        if (options.EnableHttp)
        {
            try
            {
                attached.Http = LogHttpHost.Attach(hub, options.HttpPort, options.MarkerPrefix);
                attached._hosts.Add(attached.Http);
                any = true;
            }
            catch
            {
                // ignore bind failures
            }
        }

        if (options.EnableIpc)
        {
            try
            {
                attached.Ipc = LogLocalIpcHost.Attach(hub, options.IpcAddress, options.MarkerPrefix);
                attached._hosts.Add(attached.Ipc);
                any = true;
            }
            catch
            {
                // ignore bind failures
            }
        }

        return any ? attached : null;
    }

    public async ValueTask DisposeAsync()
    {
        for (var i = _hosts.Count - 1; i >= 0; i--)
        {
            try { await _hosts[i].DisposeAsync().ConfigureAwait(false); }
            catch { /* ignore */ }
        }

        _hosts.Clear();
    }
}
