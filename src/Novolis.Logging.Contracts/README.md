<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-logging">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Logging.Contracts

Remote log line **DTOs**, JSON wire helpers, and a **scope stack bridge** for hosts that fan out to multiple logging sinks (named pipe, HTTP ingest, SSE).

One UTF-8 JSON object per line on the wire. Scope dictionaries from `ILogger.BeginScope` merge once across providers via `LoggingScopeStack`.

## Install

```bash
dotnet add package Novolis.Logging.Contracts
```

Depends on `Microsoft.Extensions.Logging.Abstractions`.

## Quick start — wire format

```csharp
using Novolis.Logging.Contracts;

var line = new RemoteLogLine
{
    T = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    L = (int)LogLevel.Information,
    C = "MyApp.Service",
    M = "Started",
    Scope = new Dictionary<string, string> { ["runId"] = "abc" },
};

var bytes = RemoteLogJson.SerializeLine(line);
var parsed = RemoteLogJson.TryDeserializeLine(System.Text.Encoding.UTF8.GetString(bytes));
```

JSON property names: `t` (unix ms), `l` (level), `c` (category), `m` (message), `x` (exception), `sid` (session), `s` (scope map).

## Quick start — scope bridge

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Contracts;

services.AddLogging(b => b.AddLoggingScopeStackBridge());
// Any sink can read LoggingScopeStack.CurrentMerged when formatting RemoteLogLine
```

`LoggingScopeBridgeLoggerProvider` is silent (never writes logs); it exists so scopes are pushed exactly once per logical scope.

## API

| Type | Role |
|------|------|
| `RemoteLogLine` | One log record on the wire |
| `RemoteLogJson` | Serialize/deserialize lines; `Options`, `Configure` |
| `LoggingScopeStack` | `CurrentMerged` scope dictionary (AsyncLocal stack) |
| `LoggingScopeBridgeLoggerProvider` | Silent provider that pushes scopes |
| `LoggingScopeStackBridgeExtensions` | `AddLoggingScopeStackBridge()` on `ILoggingBuilder` |

## Related

| Package | Role |
|---------|------|
| `Novolis.Logging.Core` | Ring buffer + `ILogger` sink |
| `Novolis.Logging.Transports` | HTTP ingest/SSE + LocalIpc |
| `Novolis.Logging.Faster` | Durable FasterLog sink |
| `Novolis.Logging.Agent` | Agent Surface for agents |

Restore from **nuget.org** + **GitHub Packages** (`Novolis.*` at `2026.1.*`).

