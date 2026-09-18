<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-logging">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Logging.Core

In-process **remote log hub**: ring buffer, fan-out, and an `ILoggerProvider` that writes `RemoteLogLine` records (with scope merge from `Novolis.Logging.Contracts`).

## Install

```bash
dotnet add package Novolis.Logging.Core
```

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Core;

var services = new ServiceCollection();
services.AddRemoteLogging(o =>
{
    o.Capacity = 4096;
    o.SessionId = "run-1";
    o.MinLevel = LogLevel.Debug;
});

var sp = services.BuildServiceProvider();
var hub = sp.GetRequiredService<RemoteLogHub>();
var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("demo");

log.LogInformation("hello {Name}", "world");
foreach (var line in hub.Tail(10))
    Console.WriteLine($"{line.C}: {line.M}");
```

## API

| Type | Role |
|------|------|
| `RemoteLogHub` | Ring buffer, `Append` / `Tail` / `Clear`, `LineAppended`, `BuildStatusLines` |
| `RemoteLogOptions` | Capacity, session id, min level, snapshot tail count |
| `RemoteLogLoggerProvider` | `ILogger` → hub |
| `AddRemoteLogging` / `AddRemoteLogHub` | DI helpers |

Pair with `Novolis.Logging.Transports` for HTTP/SSE/LocalIpc ingest, `Novolis.Logging.Faster` for durable FasterLog persistence, and `Novolis.Logging.Agent` for Agent Surface.

Restore from **nuget.org** + **GitHub Packages** (`Novolis.*` at `2026.1.*`).
