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
            journal.WriteException("unit", new ArgumentException("bad argument"));

            var content = await File.ReadAllTextAsync(journal.GetRecentFiles()[0]);
            await Assert.That(content).Contains("Failed operation.");
            await Assert.That(content).Contains("boom");
            await Assert.That(content).Contains("bad argument");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
