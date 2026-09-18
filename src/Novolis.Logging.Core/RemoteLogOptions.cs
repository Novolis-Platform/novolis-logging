using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Core;

/// <summary>Options for the in-process remote log hub and <see cref="ILogger"/> sink.</summary>
public sealed class RemoteLogOptions
{
    /// <summary>Ring-buffer capacity (oldest lines drop when full).</summary>
    public int Capacity { get; set; } = 2048;

    /// <summary>Optional session id stamped on every line from the local provider.</summary>
    public string? SessionId { get; set; }

    /// <summary>Minimum level accepted by the local <see cref="ILogger"/> provider.</summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Trace;

    /// <summary>How many recent lines to include in agent snapshots.</summary>
    public int SnapshotTailCount { get; set; } = 50;
}
