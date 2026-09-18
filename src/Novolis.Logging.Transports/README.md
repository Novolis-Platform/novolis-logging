<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-logging">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Logging.Transports

HTTP ingest / SSE tail and **LocalIpc** hosts over a shared [`RemoteLogHub`](../Novolis.Logging.Core/README.md). Depends on `Novolis.Transports.LocalIpc`.

## Install

```bash
dotnet add package Novolis.Logging.Transports
```

## Routes (HTTP)

| Method | Path | Body |
|--------|------|------|
| `POST` | `/log/ingest` | NDJSON `RemoteLogLine` |
| `GET` | `/log/tail?count=N` | NDJSON response |
| `GET` | `/log/events` | SSE `event: log` |

Default bind: `http://127.0.0.1:18869/`. Marker: `%TEMP%/novolis-logging-transport.http`.

## LocalIpc

Frame names: `log.ingest`, `log.tail`, `log.subscribe`; push events use `log.line`. Default address: `novolis-logging`.

## Quick start

```csharp
using Novolis.Logging.Core;
using Novolis.Logging.Transports;

var hub = new RemoteLogHub();
await using var transports = LogTransports.Attach(hub, new LogTransportOptions
{
    HttpPort = 18869,
    EnableIpc = true,
});
```

For Agent Surface consumption, use `Novolis.Logging.Agent`.

Restore from **nuget.org** + **GitHub Packages** (`Novolis.*` at `2026.1.*`).
