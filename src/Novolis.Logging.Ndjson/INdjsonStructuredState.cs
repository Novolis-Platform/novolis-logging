namespace Novolis.Logging.Ndjson;

/// <summary>Optional structured state contract for a log state's message and named values.</summary>
public interface INdjsonStructuredState
{
    /// <summary>Short message associated with the state.</summary>
    string Message { get; }

    /// <summary>Named values associated with the state.</summary>
    IReadOnlyDictionary<string, object?>? Properties { get; }
}
