namespace Novolis.Logging.Transports;

/// <summary>Bind options for logging HTTP and LocalIpc hosts.</summary>
public sealed class LogTransportOptions
{
    public bool EnableHttp { get; set; } = true;

    public bool EnableIpc { get; set; } = true;

    /// <summary>HTTP port for <c>/log/*</c> routes (default 18869).</summary>
    public int HttpPort { get; set; } = 18869;

    /// <summary>LocalIpc pipe/socket address (default <c>novolis-logging</c>).</summary>
    public string IpcAddress { get; set; } = "novolis-logging";

    /// <summary>Marker file prefix under <c>%TEMP%</c> (default <c>novolis-logging-transport</c>).</summary>
    public string MarkerPrefix { get; set; } = "novolis-logging-transport";
}
