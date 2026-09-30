using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Diagnostics;

/// <summary>Service-registration extensions for durable diagnostic logging.</summary>
public static class DiagnosticJournalServiceCollectionExtensions
{
    /// <summary>Registers a pre-created journal and uses it as an <see cref="ILogger"/> provider.</summary>
    public static IServiceCollection AddDiagnosticFileLogging(
        this IServiceCollection services,
        IDiagnosticJournal journal)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(journal);

        services.TryAddSingleton(journal);
        services.TryAddSingleton<IDiagnosticJournal>(journal);
        services.AddLogging(logging => logging.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider>(new DiagnosticJournalLoggerProvider(journal))));
        return services;
    }
}
