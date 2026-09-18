using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Novolis.Agent.Core;
using Novolis.Logging.Agent;
using Novolis.Logging.Contracts;
using Novolis.Logging.Core;

namespace Novolis.Logging.Unit;

public sealed class LoggingAgentHostTests
{
    [Test]
    public async Task Snapshot_Exposes_Tail()
    {
        var hub = new RemoteLogHub();
        using var host = new LoggingAgentHost(hub);
        hub.Append(new RemoteLogLine { M = "x", L = (int)LogLevel.Information });

        var snap = host.Snapshot();
        await Assert.That(snap.StatusLines["log.count"]).IsEqualTo("1");
        await Assert.That(snap.StatusLines["log.latest"]).Contains("\"m\":\"x\"");
    }

    [Test]
    public async Task Subscribe_Raises_Changed_On_Append()
    {
        var hub = new RemoteLogHub();
        using var host = new LoggingAgentHost(hub);
        AgentChangedEvent? seen = null;
        host.Changed += e => seen = e;
        host.Subscribe();

        hub.Append(new RemoteLogLine { M = "pushed", L = (int)LogLevel.Warning });

        await Assert.That(seen).IsNotNull();
        await Assert.That(seen!.Reason).IsEqualTo("log");
        await Assert.That(seen.Snapshot!.StatusLines["log.latest"]).Contains("pushed");
    }

    [Test]
    public async Task SetLevel_And_Clear_Commands()
    {
        var hub = new RemoteLogHub(Options.Create(new RemoteLogOptions { MinLevel = LogLevel.Trace }));
        using var host = new LoggingAgentHost(hub);
        hub.Append(new RemoteLogLine { M = "keep", L = (int)LogLevel.Information });

        var set = host.Execute(new AgentCommand
        {
            ActionId = LoggingAgentActionIds.SetLevel,
            Params = { ["level"] = "Error" },
        });
        await Assert.That(set.Ok).IsTrue();
        await Assert.That(hub.MinLevel).IsEqualTo(LogLevel.Error);

        var clear = host.Execute(new AgentCommand { ActionId = LoggingAgentActionIds.Clear });
        await Assert.That(clear.Ok).IsTrue();
        await Assert.That(hub.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Hello_SurfaceId_Is_Logging()
    {
        var hub = new RemoteLogHub();
        using var host = new LoggingAgentHost(hub);
        await Assert.That(host.Hello().SurfaceId).IsEqualTo("logging");
    }

    [Test]
    public async Task Actions_Continue_Unknown_And_BadLevel()
    {
        var hub = new RemoteLogHub();
        using var host = new LoggingAgentHost(hub);

        var actions = host.Actions();
        await Assert.That(actions.Actions.Count).IsEqualTo(2);
        await Assert.That(actions.Actions.Any(a => a.Id == LoggingAgentActionIds.SetLevel)).IsTrue();

        var cont = host.Continue();
        await Assert.That(cont.Ok).IsTrue();

        var unknown = host.Execute(new AgentCommand { ActionId = "nope" });
        await Assert.That(unknown.Ok).IsFalse();

        var bad = host.Execute(new AgentCommand
        {
            ActionId = LoggingAgentActionIds.SetLevel,
            Params = { ["value"] = "not-a-level" },
        });
        await Assert.That(bad.Ok).IsFalse();

        var viaValue = host.Execute(new AgentCommand
        {
            ActionId = LoggingAgentActionIds.SetLevel,
            Params = { ["value"] = "Warning" },
        });
        await Assert.That(viaValue.Ok).IsTrue();
        await Assert.That(hub.MinLevel).IsEqualTo(LogLevel.Warning);
    }

    [Test]
    public async Task Append_Without_Subscribe_Does_Not_Raise_Changed()
    {
        var hub = new RemoteLogHub();
        using var host = new LoggingAgentHost(hub);
        var raised = false;
        host.Changed += _ => raised = true;
        hub.Append(new RemoteLogLine { M = "quiet", L = (int)LogLevel.Information });
        await Assert.That(raised).IsFalse();
    }

    [Test]
    public async Task Dispose_Twice_Is_Safe()
    {
        var hub = new RemoteLogHub();
        var host = new LoggingAgentHost(hub);
        host.Dispose();
        host.Dispose();
        await Assert.That(hub.Count).IsEqualTo(0);
    }

    [Test]
    public async Task NullHub_And_BlankActionId_Fail()
    {
        await Assert.That(() => new LoggingAgentHost(null!)).Throws<ArgumentNullException>();

        var hub = new RemoteLogHub();
        using var host = new LoggingAgentHost(hub);
        var blank = host.Execute(new AgentCommand { ActionId = "  " });
        await Assert.That(blank.Ok).IsFalse();
    }
}
