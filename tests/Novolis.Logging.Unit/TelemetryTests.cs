using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Telemetry;
using TUnit.Core;

namespace Novolis.Logging.Unit;

public sealed class TelemetryTests
{
    [Test]
    public async Task Telemetry_provider_persists_only_explicit_events()
    {
        var directory = Directory.CreateTempSubdirectory("novolis-telemetry-");
        try
        {
            var path = Path.Combine(directory.FullName, "telemetry.ndjson");
            var services = new ServiceCollection();
            services.AddLogging(builder =>
                builder.AddNdjsonTelemetry(options => options.FilePath = path));
            await using var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("test");

            logger.LogInformation("ordinary application log");
            TelemetryLog.Record(
                logger,
                "speech.azure.call",
                680,
                "characters",
                new Dictionary<string, object?> { ["voice"] = "en-US-AvaNeural" });

            var journal = new TelemetryJournal(new NdjsonTelemetryOptions
            {
                FilePath = path,
            });
            var events = new List<TelemetryEvent>();
            await foreach (var item in journal.ReadAsync())
                events.Add(item);

            await Assert.That(events).Count().IsEqualTo(1);
            await Assert.That(events[0].Name).IsEqualTo("speech.azure.call");
            await Assert.That(events[0].Value).IsEqualTo(680);
            await Assert.That(events[0].Properties!["voice"]?.ToString()).IsEqualTo("en-US-AvaNeural");
            journal.Dispose();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
