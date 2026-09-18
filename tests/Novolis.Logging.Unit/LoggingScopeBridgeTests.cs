using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Contracts;

namespace Novolis.Logging.Unit;

public sealed class LoggingScopeBridgeTests
{
    [Test]
    public async Task BeginScope_MergesNestedDictionaries()
    {
        var provider = new LoggingScopeBridgeLoggerProvider();
        var logger = provider.CreateLogger("test");

        await Assert.That(LoggingScopeStack.CurrentMerged).IsNull();

        using (logger.BeginScope(new Dictionary<string, object> { ["a"] = "1" }))
        {
            await Assert.That(LoggingScopeStack.CurrentMerged!["a"]).IsEqualTo("1");

            using (logger.BeginScope(new Dictionary<string, object> { ["b"] = "2", ["a"] = "override" }))
            {
                await Assert.That(LoggingScopeStack.CurrentMerged!["a"]).IsEqualTo("override");
                await Assert.That(LoggingScopeStack.CurrentMerged["b"]).IsEqualTo("2");
            }

            await Assert.That(LoggingScopeStack.CurrentMerged!["a"]).IsEqualTo("1");
            await Assert.That(LoggingScopeStack.CurrentMerged.ContainsKey("b")).IsFalse();
        }

        await Assert.That(LoggingScopeStack.CurrentMerged).IsNull();
    }

    [Test]
    public async Task BeginScope_NonEnumerableState_IsNoOp()
    {
        var logger = new LoggingScopeBridgeLoggerProvider().CreateLogger("x");
        using (logger.BeginScope("plain-string"))
            await Assert.That(LoggingScopeStack.CurrentMerged).IsNull();
    }

    [Test]
    public async Task BridgeLogger_IsNeverEnabled()
    {
        var logger = new LoggingScopeBridgeLoggerProvider().CreateLogger("x");
        await Assert.That(logger.IsEnabled(LogLevel.Critical)).IsFalse();
        logger.LogInformation("ignored");
    }

    [Test]
    public async Task BeginScope_NullValue_And_DoubleDispose()
    {
        var provider = new LoggingScopeBridgeLoggerProvider();
        var logger = provider.CreateLogger("test");
        var scope = logger.BeginScope(new Dictionary<string, object> { ["k"] = null! });
        await Assert.That(LoggingScopeStack.CurrentMerged!["k"]).IsEqualTo("");
        scope!.Dispose();
        scope.Dispose();
        await Assert.That(LoggingScopeStack.CurrentMerged).IsNull();
    }

    [Test]
    public async Task AddLoggingScopeStackBridge_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddLoggingScopeStackBridge());
        await using var sp = services.BuildServiceProvider();
        var providers = sp.GetServices<ILoggerProvider>().ToArray();
        await Assert.That(providers.OfType<LoggingScopeBridgeLoggerProvider>().Any()).IsTrue();
    }
}
