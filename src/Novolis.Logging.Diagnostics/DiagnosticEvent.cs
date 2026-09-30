namespace Novolis.Logging.Diagnostics;

/// <summary>Logger state whose message stays short and whose values keep their names.</summary>
internal sealed class DiagnosticEvent
{
    public DiagnosticEvent(string message, Dictionary<string, object?>? properties)
    {
        Message = message;
        Properties = properties;
    }

    public string Message { get; }

    public Dictionary<string, object?>? Properties { get; }
}
