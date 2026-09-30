using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Diagnostics;

namespace Novolis.Logging.Unit;

public sealed class DiagnosticJournalTests
{
    [Test]
    public async Task AddDiagnosticFileLogging_Writes_Logger_And_Exception_Records()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"novolis-logging-{Guid.NewGuid():N}");
        try
        {
            using var journal = new DiagnosticJournal(new DiagnosticJournalOptions
            {
                DirectoryPath = directory,
                ApplicationName = "LoggingUnit",
            });
            var services = new ServiceCollection();
            services.AddDiagnosticFileLogging(journal);
            await using var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("unit");

            logger.LogError(new InvalidOperationException("boom"), "Failed operation.");
            DiagnosticLog.Information(
                logger,
                "Synthesized audio.",
                new DiagnosticProperties()
                    .Set("characters", 680)
                    .Set("voice", "en-US-AvaNeural")
                    .Set("elapsedMs", 4964)
                    .Set("bytes", 283392));
            journal.WriteException("unit", new ArgumentException("bad argument"));

            var content = await File.ReadAllTextAsync(journal.GetRecentFiles()[0]);
            await Assert.That(content).Contains("\"message\":\"Failed operation.\"");
            await Assert.That(content).Contains("\"level\":\"Error\"");
            await Assert.That(content).Contains("\"message\":\"Synthesized audio.\"");
            await Assert.That(content).Contains("\"characters\":680");
            await Assert.That(content).Contains("\"voice\":\"en-US-AvaNeural\"");
            await Assert.That(content).Contains("\"elapsedMs\":4964");
            await Assert.That(content).Contains("\"bytes\":283392");
            await Assert.That(content).Contains("\"time\":\"20");
            await Assert.That(content).Contains("boom");
            await Assert.That(content).Contains("bad argument");

            var summary = journal.ReadRecentSummary();
            await Assert.That(summary).Contains("Failed operation.");
            await Assert.That(summary).Contains("Unhandled exception.");
            await Assert.That(summary).Contains("Diagnostic journal started");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
