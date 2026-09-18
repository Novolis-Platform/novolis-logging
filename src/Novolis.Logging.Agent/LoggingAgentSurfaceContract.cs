using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace Novolis.Logging.Agent;

/// <summary>Attributed Agent Surface for remote log tail / subscribe / level filter.</summary>
[AgentSurface("logging",
    HttpPort = 18867,
    TcpPort = 18868,
    EnableEnv = "NOVOLIS_LOGGING",
    MarkerPrefix = "novolis-logging",
    Description = "Remote log tail, subscribe, and level filter")]
[AgentAction(LoggingAgentActionIds.SetLevel,
    Summary = "Minimum log level",
    Params = "level|Trace,Debug,Information,Warning,Error,Critical")]
[AgentAction(LoggingAgentActionIds.Clear, Summary = "Clear in-memory buffer")]
public interface ILoggingAgentSurface : IAgentHost;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Attribute reflection glue for AgentSurface host attach
public static class LoggingAgentSurfaceContract
{
    public static AgentSurfaceDefinition Definition { get; } =
        AgentSurfaceDefinition.From<ILoggingAgentSurface>();
}
