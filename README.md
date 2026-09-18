<!-- novolis-package-index:start -->
> **GitHub Packages shows this repository README on every package page** (upstream limitation).
> Open the **package README** for install and quick start — embedded in each .nupkg and linked below.

## Published packages

| Package | Install | Package README |
|---------|---------|----------------|
| `Novolis.Logging.Agent` | `dotnet add package Novolis.Logging.Agent` | [README](https://github.com/Novolis-Platform/novolis-logging/blob/main/src/Novolis.Logging.Agent/README.md) |
| `Novolis.Logging.Contracts` | `dotnet add package Novolis.Logging.Contracts` | [README](https://github.com/Novolis-Platform/novolis-logging/blob/main/src/Novolis.Logging.Contracts/README.md) |
| `Novolis.Logging.Core` | `dotnet add package Novolis.Logging.Core` | [README](https://github.com/Novolis-Platform/novolis-logging/blob/main/src/Novolis.Logging.Core/README.md) |
| `Novolis.Logging.Diagnostics` | `dotnet add package Novolis.Logging.Diagnostics` | [README](https://github.com/Novolis-Platform/novolis-logging/blob/main/src/Novolis.Logging.Diagnostics/README.md) |
| `Novolis.Logging.Faster` | `dotnet add package Novolis.Logging.Faster` | [README](https://github.com/Novolis-Platform/novolis-logging/blob/main/src/Novolis.Logging.Faster/README.md) |
| `Novolis.Logging.Transports` | `dotnet add package Novolis.Logging.Transports` | [README](https://github.com/Novolis-Platform/novolis-logging/blob/main/src/Novolis.Logging.Transports/README.md) |

For NuGet.org and Visual Studio, the **embedded** README.md inside each package is authoritative.

<!-- novolis-package-index:end -->

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
  <strong>Logging and diagnostics</strong><br/>
  Logging and durable diagnostics shared across Novolis libraries and apps.
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-logging/"><img src="https://img.shields.io/badge/docs-portfolio-0a7ea3" alt="docs"/></a>
  <a href="https://github.com/Novolis-Platform/novolis-logging/actions"><img src="https://img.shields.io/github/actions/workflow/status/Novolis-Platform/novolis-logging/merge.yml?branch=main&label=merge&logo=github" alt="merge"/></a>
  <a href="https://github.com/orgs/Novolis-Platform/packages?repo_name=novolis-logging"><img src="https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget" alt="packages"/></a>
  <a href="https://github.com/Novolis-Platform"><img src="https://img.shields.io/badge/org-Novolis--Platform-111827" alt="org"/></a>
</p>

<p align="center">
  <a href="https://novolis-platform.github.io/.github/novolis-logging/">Docs</a>
  ·
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

