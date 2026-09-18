using System.Text.Json.Serialization;

namespace Novolis.Logging.Contracts;

/// <summary>One UTF-8 JSON line on the wire between remote client and host ingest.</summary>
public sealed class RemoteLogLine
{
    [JsonPropertyName("t")]
    public long T { get; set; }

    [JsonPropertyName("l")]
    public int L { get; set; }

    [JsonPropertyName("c")]
    public string C { get; set; } = "";

    [JsonPropertyName("m")]
    public string M { get; set; } = "";

    [JsonPropertyName("x")]
    public string? X { get; set; }

    [JsonPropertyName("sid")]
    public string? SessionId { get; set; }

    [JsonPropertyName("s")]
    public Dictionary<string, string>? Scope { get; set; }
}
