using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Contracts;
using Novolis.Logging.Core;

namespace Novolis.Logging.Unit;

public sealed class RemoteLogLoggerProviderTests
{
    [Test]
    public async Task AddRemoteLogging_Writes_Scoped_Lines()
    {
        var services = new ServiceCollection();
        services.AddRemoteLogging(o => o.SessionId = "s1");
        await using var sp = services.BuildServiceProvider();
        var hub = sp.GetRequiredService<RemoteLogHub>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("unit");

        using (logger.BeginScope(new Dictionary<string, object> { ["run"] = "r1" }))
            logger.LogInformation("hello");

        var line = hub.Tail(1)[0];
        await Assert.That(line.M).IsEqualTo("hello");
        await Assert.That(line.SessionId).IsEqualTo("s1");
        await Assert.That(line.Scope!["run"]).IsEqualTo("r1");
    }

    [Test]
    public async Task Provider_NullHub_Throws_And_MinLevel_Filters()
    {
        await Assert.That(() => new RemoteLogLoggerProvider(null!)).Throws<ArgumentNullException>();

        var hub = new RemoteLogHub { MinLevel = LogLevel.Error };
        using var provider = new RemoteLogLoggerProvider(hub);
        var logger = provider.CreateLogger("t");
        logger.LogDebug("skipped");
        logger.Log(LogLevel.None, default, "none", null, static (s, _) => s);
        logger.LogError(new InvalidOperationException("boom"), "fail");
        await Assert.That(hub.Count).IsEqualTo(1);
        await Assert.That(hub.Tail()[0].X).Contains("boom");
    }
}
