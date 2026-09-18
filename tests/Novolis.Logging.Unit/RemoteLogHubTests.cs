using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Novolis.Logging.Contracts;
using Novolis.Logging.Core;

namespace Novolis.Logging.Unit;

public sealed class RemoteLogHubTests
{
    [Test]
    public async Task Append_And_Tail_Respects_Capacity()
    {
        var hub = new RemoteLogHub(Options.Create(new RemoteLogOptions { Capacity = 3 }));
        for (var i = 0; i < 5; i++)
            hub.Append(new RemoteLogLine { M = i.ToString(), L = (int)LogLevel.Information });

        var tail = hub.Tail(10);
        await Assert.That(hub.Count).IsEqualTo(3);
        await Assert.That(tail.Select(l => l.M).ToArray()).IsEquivalentTo(["2", "3", "4"]);
    }

    [Test]
    public async Task Append_Stamps_SessionId_From_Hub()
    {
        var hub = new RemoteLogHub { SessionId = "hub-session" };
        hub.Append(new RemoteLogLine { M = "x", L = (int)LogLevel.Information });
        await Assert.That(hub.Tail()[0].SessionId).IsEqualTo("hub-session");
    }

    [Test]
    public async Task BuildStatusLines_EmptyHub_Has_Empty_Tail()
    {
        var hub = new RemoteLogHub();
        var status = hub.BuildStatusLines();
        await Assert.That(status["log.count"]).IsEqualTo("0");
        await Assert.That(status["log.tail"]).IsEqualTo("");
        await Assert.That(status["log.latest"]).IsEqualTo("");
    }

    [Test]
    public async Task MinLevel_Filters_Append()
    {
        var hub = new RemoteLogHub(Options.Create(new RemoteLogOptions { MinLevel = LogLevel.Warning }));
        hub.Append(new RemoteLogLine { M = "info", L = (int)LogLevel.Information });
        hub.Append(new RemoteLogLine { M = "warn", L = (int)LogLevel.Warning });

        await Assert.That(hub.Count).IsEqualTo(1);
        await Assert.That(hub.Tail()[0].M).IsEqualTo("warn");
    }

    [Test]
    public async Task BuildStatusLines_Includes_Tail_Ndjson()
    {
        var hub = new RemoteLogHub();
        hub.Append(new RemoteLogLine { M = "a", C = "c", L = (int)LogLevel.Information, T = 1 });
        var status = hub.BuildStatusLines();
        await Assert.That(status["log.count"]).IsEqualTo("1");
        await Assert.That(status["log.latest"]).Contains("\"m\":\"a\"");
    }

    [Test]
    public async Task AddRemoteLogHub_Registers_Provider()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging(b => b.AddRemoteLogHub());
        await using var sp = services.BuildServiceProvider();
        var hub = sp.GetRequiredService<RemoteLogHub>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("t");
        logger.LogInformation("via-hub");
        await Assert.That(hub.Count).IsGreaterThanOrEqualTo(1);
    }
}
