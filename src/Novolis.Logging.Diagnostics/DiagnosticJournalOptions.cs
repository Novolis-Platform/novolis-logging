using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Diagnostics;

/// <summary>Options for a bounded, app-private diagnostic journal.</summary>
public sealed class DiagnosticJournalOptions
{
    /// <summary>Directory where journal files are stored.</summary>
    public required string DirectoryPath { get; init; }

    /// <summary>Stable product name written to the journal header.</summary>
    public required string ApplicationName { get; init; }

    /// <summary>Maximum size of one journal file before a new file is started.</summary>
    public long MaximumFileBytes { get; init; } = 1_048_576;

    /// <summary>Maximum number of recent journal files retained.</summary>
    public int RetainedFileCount { get; init; } = 5;
}
