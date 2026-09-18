<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-logging">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Logging.Faster

Durable **FasterLog** sink for `RemoteLogLine` records using [Microsoft FASTER](https://microsoft.github.io/FASTER/) (`Microsoft.FASTER.Core`). Complements the in-memory `RemoteLogHub` ring buffer with recoverable on-disk persistence and group commit.

## Install

```bash
dotnet add package Novolis.Logging.Faster
```

Depends on `Microsoft.FASTER.Core` and `Novolis.Logging.Core`.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Core;
using Novolis.Logging.Faster;

var dir = Path.Combine(Path.GetTempPath(), "novolis-faster-log");
var services = new ServiceCollection();
services.AddRemoteLogging(o => o.SessionId = "run-1");
services.AddFasterRemoteLog(o =>
{
    o.Directory = dir;
    o.CommitInterval = TimeSpan.FromMilliseconds(25);
});

await using var sp = services.BuildServiceProvider();
var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("demo");
var store = sp.GetRequiredService<FasterRemoteLogStore>();

log.LogInformation("persisted");
await store.CommitAsync();

foreach (var line in await store.ReadCommittedAsync(maxCount: 10))
    Console.WriteLine($"{line.C}: {line.M}");
```

Without DI:

```csharp
using Novolis.Logging.Contracts;
using Novolis.Logging.Core;
using Novolis.Logging.Faster;

var hub = new RemoteLogHub();
await using var store = new FasterRemoteLogStore(new FasterRemoteLogOptions
{
    Directory = @"d:\logs\app",
    AutoCommit = true,
});
store.Attach(hub);

hub.Append(new RemoteLogLine { M = "hello", L = 2, C = "demo" });
await store.CommitAsync();
```

## API

| Type | Role |
|------|------|
| `FasterRemoteLogStore` | FasterLog enqueue / commit / scan; optional hub attach |
| `FasterRemoteLogOptions` | Directory, auto-commit, attach, page/memory bits |
| `AddFasterRemoteLog` | DI registration |

Restore from **nuget.org** + **GitHub Packages** (`Novolis.*` at `2026.1.*`).
