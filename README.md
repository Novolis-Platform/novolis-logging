<!-- novolis-marketing:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-brand-transparent.svg" width="360" alt="Novolis"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/banners/novolis-logging.svg" width="100%" alt="novolis-logging"/>
</p>

<p align="center">
  <strong>Logging building blocks</strong><br/>
  Logging helpers shared across Novolis libraries and apps.
</p>

<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-logging/actions"><img src="https://img.shields.io/github/actions/workflow/status/Novolis-Platform/novolis-logging/merge.yml?branch=main&label=merge&logo=github" alt="merge"/></a>
  <a href="https://github.com/orgs/Novolis-Platform/packages?repo_name=novolis-logging"><img src="https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget" alt="packages"/></a>
  <a href="https://github.com/Novolis-Platform"><img src="https://img.shields.io/badge/org-Novolis--Platform-111827" alt="org"/></a>
</p>

<p align="center">
  <a href="https://nuget.pkg.github.com/Novolis-Platform/index.json"><code>https://nuget.pkg.github.com/Novolis-Platform/index.json</code></a>
  ·
  <a href="https://github.com/Novolis-Platform/.github/blob/main/profile/README.md">Org landing</a>
  ·
  <a href="https://github.com/Novolis-Platform/novolis-governance">Governance</a>
</p>

---
<!-- novolis-marketing:end -->
# novolis-logging

Logging helpers shared across Novolis libraries and apps.

## Packages

| Package | Role |
|---------|------|
| [`Novolis.Logging.Contracts`](src/Novolis.Logging.Contracts) | Wire DTOs (`RemoteLogLine`) + scope stack bridge |
| [`Novolis.Logging.Core`](src/Novolis.Logging.Core) | In-process hub / ring buffer / `ILogger` provider |
| [`Novolis.Logging.Diagnostics`](src/Novolis.Logging.Diagnostics) | Bounded app-private diagnostic journal / `ILogger` provider |
| [`Novolis.Logging.Transports`](src/Novolis.Logging.Transports) | HTTP ingest + SSE + LocalIpc (`Novolis.Transports.LocalIpc`) |
| [`Novolis.Logging.Faster`](src/Novolis.Logging.Faster) | Durable disk sink via Microsoft FASTER `FasterLog` |
| [`Novolis.Logging.Agent`](src/Novolis.Logging.Agent) | Agent Surface (`logging`) for agent consumers |

Agents attach via `LoggingSurface.Attach(hub)` and read logs with `agent.snapshot` / `agent.subscribe` (SSE `/agent/events`). Bulk ingest remains on `/log/ingest`.

## Get started

Configure GitHub Packages once (from a sibling novolis-governance checkout):

```powershell
pwsh -File d:\novolis\novolis-governance\scripts\configure-gpr-user-nuget.ps1
```

```powershell
dotnet build d:\novolis\novolis-logging\Novolis.Logging.slnx -p:NovolisUseProjectReferences=true
dotnet test d:\novolis\novolis-logging\tests\Novolis.Logging.Unit\Novolis.Logging.Unit.csproj -p:NovolisUseProjectReferences=true
```

See [Novolis-Platform](https://github.com/Novolis-Platform) for the full ecosystem.

