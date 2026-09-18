using Novolis.Agent.Core;
using Novolis.Agent.Surface;
using Novolis.Logging.Core;
using Novolis.Logging.Transports;

namespace Novolis.Logging.Agent;

/// <summary>
/// Attaches the logging Agent Surface (and optionally <see cref="LogTransports"/>) to a shared hub
/// so agents can consume logs via <c>agent.snapshot</c> / <c>agent.subscribe</c> (+ SSE).
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // AgentSurface / LogTransports network host attach
public sealed class LoggingSurface : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _owned = new();
    private LoggingAgentHost? _host;

    private LoggingSurface()
    {
    }

    public LoggingAgentHost Host =>
        _host ?? throw new InvalidOperationException("Surface was not attached.");

    public AgentSurface? Agent { get; private set; }

    public LogTransports? Transports { get; private set; }

    public string? AgentHttpBaseUrl => Agent?.HttpBaseUrl;

    public string? LogHttpBaseUrl => Transports?.HttpBaseUrl;

    /// <summary>
    /// Attach Agent Surface transports for <see cref="ILoggingAgentSurface"/>.
    /// When <paramref name="attachLogTransports"/> is true, also binds HTTP/LocalIpc <c>/log/*</c> hosts.
    /// </summary>
    public static LoggingSurface Attach(
        RemoteLogHub hub,
        AgentAttachOptions? agentOptions = null,
        LogTransportOptions? logTransportOptions = null,
        bool attachLogTransports = true)
    {
        ArgumentNullException.ThrowIfNull(hub);

        var surface = new LoggingSurface
        {
            _host = new LoggingAgentHost(hub),
        };

        var agent = AgentSurface.AttachAll(
            surface._host,
            LoggingAgentSurfaceContract.Definition,
            agentOptions ?? new AgentAttachOptions
            {
                EnableIpc = true,
                EnableHttp = true,
                EnableTcp = true,
                EnableRpc = false,
                HttpPort = 18867,
                TcpPort = 18868,
            });

        if (agent is not null)
        {
            surface.Agent = agent;
            surface._owned.Add(agent);
        }

        if (attachLogTransports)
        {
            var transports = LogTransports.Attach(hub, logTransportOptions);
            if (transports is not null)
            {
                surface.Transports = transports;
                surface._owned.Add(transports);
            }
        }

        return surface;
    }

    /// <summary>
    /// Attach when <c>NOVOLIS_LOGGING</c> (or related) env vars enable the surface.
    /// Returns <c>null</c> when the Agent Surface env gate is off.
    /// </summary>
    public static LoggingSurface? TryAttachFromEnvironment(
        RemoteLogHub hub,
        bool attachLogTransports = true,
        LogTransportOptions? logTransportOptions = null)
    {
        ArgumentNullException.ThrowIfNull(hub);

        var host = new LoggingAgentHost(hub);
        var agent = AgentSurface.TryAttachFromEnvironment(host, LoggingAgentSurfaceContract.Definition);
        if (agent is null)
        {
            host.Dispose();
            return null;
        }

        var surface = new LoggingSurface { _host = host, Agent = agent };
        surface._owned.Add(agent);

        if (attachLogTransports)
        {
            var transports = LogTransports.Attach(hub, logTransportOptions);
            if (transports is not null)
            {
                surface.Transports = transports;
                surface._owned.Add(transports);
            }
        }

        return surface;
    }

    public async ValueTask DisposeAsync()
    {
        for (var i = _owned.Count - 1; i >= 0; i--)
        {
            try { await _owned[i].DisposeAsync().ConfigureAwait(false); }
            catch { /* ignore */ }
        }

        _owned.Clear();
        _host?.Dispose();
        _host = null;
    }
}
