<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-logging">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Logging.Agent

**Agent Surface** for remote logs: agents consume the in-process hub via standard `agent.snapshot` / `agent.subscribe` (HTTP/SSE/WebSocket/LocalIpc/TCP). Depends on `Novolis.Logging.Transports`, `Novolis.Agent.Core`, and `Novolis.Agent.Surface`.

## Install

```bash
dotnet add package Novolis.Logging.Agent
```

## Surface

| | |
|--|--|
| Surface id | `logging` |
| Env enable | `NOVOLIS_LOGGING=1` |
| Agent HTTP | `:18867` |
| Agent TCP | `:18868` |
| Log HTTP (optional) | `:18869` (`/log/ingest`, `/log/events`) |

Actions: `setlevel`, `clear`.

Snapshot `StatusLines` keys: `log.count`, `log.sequence`, `log.minLevel`, `log.tail` (NDJSON), `log.latest`.

After `agent.subscribe`, each new line raises `agent.changed` with `Reason = "log"`.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Agent;
using Novolis.Logging.Core;

var services = new ServiceCollection();
services.AddRemoteLogging(o => o.SessionId = "demo");
var sp = services.BuildServiceProvider();
var hub = sp.GetRequiredService<RemoteLogHub>();

await using var surface = LoggingSurface.Attach(hub);
// Agent tools: GET {surface.AgentHttpBaseUrl}agent/snapshot
// Bulk ingest:  POST {surface.LogHttpBaseUrl}log/ingest

var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("app");
log.LogInformation("ready for agents");
```

Env-gated attach (CI / optional sidecars):

```csharp
await using var surface = LoggingSurface.TryAttachFromEnvironment(hub);
```

Restore from **nuget.org** + **GitHub Packages** (`Novolis.*` at `2026.1.*`).
