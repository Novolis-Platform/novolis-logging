namespace Novolis.Logging.Faster;

/// <summary>Options for the Microsoft FASTER <c>FasterLog</c> durable remote-log store.</summary>
public sealed class FasterRemoteLogOptions
{
    /// <summary>Directory for FasterLog segments and commit metadata. Required.</summary>
    public string Directory { get; set; } = "";

    /// <summary>
    /// When true (default), subscribe to <see cref="Core.RemoteLogHub.LineAppended"/> if a hub is available.
    /// </summary>
    public bool AttachToHub { get; set; } = true;

    /// <summary>When true (default), periodically commit enqueued entries to disk.</summary>
    public bool AutoCommit { get; set; } = true;

    /// <summary>How often auto-commit runs. Group-commit friendly; keep short for durability.</summary>
    public TimeSpan CommitInterval { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>Optional page size bits passed to FasterLog (null = library default).</summary>
    public int? PageSizeBits { get; set; }

    /// <summary>Optional memory size bits passed to FasterLog (null = library default).</summary>
    public int? MemorySizeBits { get; set; }

    /// <summary>Optional segment size bits passed to FasterLog (null = library default).</summary>
    public int? SegmentSizeBits { get; set; }
}
