using Microsoft.Extensions.Logging;
using Novolis.Logging.Ndjson;
using TUnit.Core;

namespace Novolis.Logging.Unit;

public sealed class NdjsonLoggerProviderTests
{
    [Test]
    public async Task Provider_captures_event_state_scope_and_exception()
    {
        var sink = new RecordingSink();
        using var provider = new NdjsonLoggerProvider(sink);
        var logger = provider.CreateLogger("telemetry.test");

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["operation.id"] = "op-1",
        }))
        {
            logger.LogError(
                new EventId(42, "ProbeFailed"),
                new InvalidOperationException("boom"),
                "Probe {Probe} failed",
                "alpha");
        }

        await Assert.That(sink.Records).Count().IsEqualTo(1);
        var record = sink.Records[0];
        await Assert.That(record.Level).IsEqualTo("Error");
        await Assert.That(record.Category).IsEqualTo("telemetry.test");
        await Assert.That(record.EventId).IsEqualTo(42);
        await Assert.That(record.EventName).IsEqualTo("ProbeFailed");
        await Assert.That(record.State!["operation.id"]).IsEqualTo("op-1");
        await Assert.That(record.State!["Probe"]).IsEqualTo("alpha");
        await Assert.That(record.Exception).Contains("boom");
    }

    private sealed class RecordingSink : INdjsonLogSink
    {
        public List<NdjsonLogRecord> Records { get; } = [];

        public void Append(NdjsonLogRecord record, bool flushToDisk = false) =>
            Records.Add(record);
    }
}
