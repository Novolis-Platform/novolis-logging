namespace Novolis.Logging.Transports;

/// <summary>LocalIpc frame names for the logging transport.</summary>
public static class LogMethodNames
{
    public const string Ingest = "log.ingest";
    public const string Tail = "log.tail";
    public const string Subscribe = "log.subscribe";
    public const string Line = "log.line";
}
