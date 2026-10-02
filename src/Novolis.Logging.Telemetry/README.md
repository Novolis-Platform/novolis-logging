<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-logging/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-logging/) · [Source](https://github.com/Novolis-Platform/novolis-logging)
<!-- novolis-pkg-brand:end -->

# Novolis.Logging.Telemetry

Explicit application telemetry persisted through the typed NDJSON store.

Telemetry is intentionally opt-in and separate from ordinary logs:

```csharp
builder.Logging.AddNdjsonTelemetry(options =>
{
    options.FilePath = Path.Combine(paths.RootDirectory, "telemetry.ndjson");
});

TelemetryLog.Record(logger, "speech.azure.call", 680, "characters");
```

The resulting file is readable by the shared NDJSON viewer and by ordinary
ADB/file-copy workflows when the host places it under its app data root.
