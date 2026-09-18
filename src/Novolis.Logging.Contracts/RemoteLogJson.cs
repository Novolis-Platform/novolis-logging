using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Logging.Contracts;

/// <summary>JSON contract for <see cref="RemoteLogLine"/> (named pipe, HTTP ingest, SSE).</summary>
public static class RemoteLogJson
{
    private static JsonSerializerOptions? _configured;

    public static JsonSerializerOptions Options =>
        _configured ??= CreateDefaultOptions();

    public static void Configure(JsonSerializerOptions options) =>
        _configured = options ?? throw new ArgumentNullException(nameof(options));

    public static JsonSerializerOptions CreateDefaultOptions() => new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static byte[] SerializeLine(RemoteLogLine line)
    {
        var json = JsonSerializer.Serialize(line, Options);
        return System.Text.Encoding.UTF8.GetBytes(json + '\n');
    }

    public static RemoteLogLine? TryDeserializeLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;
        try
        {
            return JsonSerializer.Deserialize<RemoteLogLine>(line, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
