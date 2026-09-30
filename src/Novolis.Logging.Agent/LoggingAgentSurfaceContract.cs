using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace Novolis.Logging.Agent;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // Attribute reflection glue for AgentSurface host attach
public static class LoggingAgentSurfaceContract
{
    public static AgentSurfaceDefinition Definition { get; } =
        AgentSurfaceDefinition.From<ILoggingAgentSurface>();
}
