using Microsoft.Extensions.Logging;
using Novolis.Agent.Core;
using Novolis.Logging.Contracts;
using Novolis.Logging.Core;

namespace Novolis.Logging.Agent;

/// <summary>
/// Agent Surface host over a <see cref="RemoteLogHub"/>: snapshot carries NDJSON tail;
/// <see cref="Subscribe"/> pushes <c>agent.changed</c> with reason <c>log</c> for each line.
/// </summary>
public sealed class LoggingAgentHost : IAgentHost, IDisposable
{
    private readonly RemoteLogHub _hub;
    private readonly object _gate = new();
    private bool _subscribed;
    private long _eventSequence;
    private bool _disposed;

    public LoggingAgentHost(RemoteLogHub hub)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _hub.LineAppended += OnLineAppended;
    }

#pragma warning disable CS0067
    public event Action<AgentDecisionEvent>? Decision;
    public event Action<AgentChangedEvent>? Changed;
    public event Action<AgentActionResultEvent>? ActionResult;
#pragma warning restore CS0067

    public AgentHello Hello() => new()
    {
        ProtocolVersion = "1.0",
        AppId = "logging",
        AppTitle = "Novolis Logging",
        ProcessId = Environment.ProcessId,
        SurfaceId = "logging",
        Description = "Remote log tail, subscribe, and level filter",
        HttpPort = 18867,
        TcpPort = 18868,
        Capabilities =
        [
            AgentMethodNames.Hello,
            AgentMethodNames.Snapshot,
            AgentMethodNames.Actions,
            AgentMethodNames.Command,
            AgentMethodNames.Continue,
            AgentMethodNames.Subscribe,
        ],
    };

    public AgentSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new AgentSnapshot
            {
                Sequence = _hub.Sequence,
                HubId = "logging",
                HubName = "Novolis Logging",
                StatusLines = _hub.BuildStatusLines(),
            };
        }
    }

    public AgentActionsResponse Actions() => new()
    {
        Actions =
        [
            new AgentAction
            {
                Id = LoggingAgentActionIds.SetLevel,
                Label = "Set level",
                Enabled = true,
                Summary = "Minimum log level",
                Params = "level|Trace,Debug,Information,Warning,Error,Critical",
            },
            new AgentAction
            {
                Id = LoggingAgentActionIds.Clear,
                Label = "Clear",
                Enabled = true,
                Summary = "Clear in-memory buffer",
            },
        ],
    };

    public AgentCommandResult Continue() =>
        new() { Ok = true, ActionId = AgentActionIds.Continue, Message = "no gate", Snapshot = Snapshot() };

    public void Subscribe()
    {
        lock (_gate)
            _subscribed = true;
    }

    public AgentCommandResult Execute(AgentCommand command)
    {
        var id = command.ActionId?.Trim() ?? "";
        try
        {
            return id switch
            {
                LoggingAgentActionIds.SetLevel => DoSetLevel(command),
                LoggingAgentActionIds.Clear => DoClear(),
                _ => Fail(id, $"unknown action '{id}'"),
            };
        }
        catch (Exception ex)
        {
            return Fail(id, ex.Message);
        }
    }

    private AgentCommandResult DoSetLevel(AgentCommand command)
    {
        var raw = command.Get("level") ?? command.Get("value");
        if (string.IsNullOrWhiteSpace(raw)
            || !Enum.TryParse<LogLevel>(raw, ignoreCase: true, out var level)
            || level == LogLevel.None)
        {
            return Fail(LoggingAgentActionIds.SetLevel, "missing level|Trace,Debug,Information,Warning,Error,Critical");
        }

        _hub.MinLevel = level;
        return Ok(LoggingAgentActionIds.SetLevel, $"minLevel {level}");
    }

    private AgentCommandResult DoClear()
    {
        _hub.Clear();
        RaiseChanged("clear");
        return Ok(LoggingAgentActionIds.Clear, "cleared");
    }

    private AgentCommandResult Ok(string actionId, string message) =>
        new()
        {
            Ok = true,
            ActionId = actionId,
            Message = message,
            Snapshot = Snapshot(),
        };

    private static AgentCommandResult Fail(string actionId, string message) =>
        new()
        {
            Ok = false,
            ActionId = actionId,
            Message = message,
            ErrorCode = "failed",
        };

    private void OnLineAppended(RemoteLogLine line)
    {
        bool subscribed;
        lock (_gate)
            subscribed = _subscribed;
        if (!subscribed)
            return;

        RaiseChanged("log", line);
    }

    private void RaiseChanged(string reason, RemoteLogLine? line = null)
    {
        var sequence = Interlocked.Increment(ref _eventSequence);
        var status = _hub.BuildStatusLines(line is null ? null : 1);
        if (line is not null)
        {
            status["log.latest"] = System.Text.Encoding.UTF8
                .GetString(RemoteLogJson.SerializeLine(line))
                .TrimEnd('\n');
            status["log.reason"] = reason;
        }

        Changed?.Invoke(new AgentChangedEvent
        {
            Sequence = sequence,
            Reason = reason,
            Snapshot = new AgentSnapshot
            {
                Sequence = _hub.Sequence,
                HubId = "logging",
                HubName = "Novolis Logging",
                StatusLines = status,
            },
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _hub.LineAppended -= OnLineAppended;
    }
}
