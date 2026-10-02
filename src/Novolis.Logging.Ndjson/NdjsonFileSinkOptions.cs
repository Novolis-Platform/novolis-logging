using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Logging.Ndjson;

/// <summary>Options for a rotating NDJSON log sink.</summary>
public sealed class NdjsonFileSinkOptions
{
    /// <summary>Directory containing rotated NDJSON files.</summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>Prefix used for generated file names.</summary>
    public string FilePrefix { get; set; } = "logs";

    /// <summary>Maximum physical file size before rotation.</summary>
    public long MaximumFileBytes { get; set; } = 4 * 1024 * 1024;

    /// <summary>Number of newest files retained.</summary>
    public int RetainedFileCount { get; set; } = 4;

    /// <summary>JSON options used to serialize log records.</summary>
    public JsonSerializerOptions JsonSerializerOptions { get; } = CreateJsonOptions();

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(DirectoryPath))
            throw new ArgumentException("A log directory is required.", nameof(DirectoryPath));
        if (string.IsNullOrWhiteSpace(FilePrefix)
            || !string.Equals(Path.GetFileName(FilePrefix), FilePrefix, StringComparison.Ordinal)
            || FilePrefix.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException("The log file prefix must be a simple name.", nameof(FilePrefix));
        }
        if (MaximumFileBytes < 4_096)
            throw new ArgumentOutOfRangeException(nameof(MaximumFileBytes));
        if (RetainedFileCount < 1)
            throw new ArgumentOutOfRangeException(nameof(RetainedFileCount));
    }

    private static JsonSerializerOptions CreateJsonOptions() => new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
