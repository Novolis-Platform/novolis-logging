<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-logging/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-logging/) · [Source](https://github.com/Novolis-Platform/novolis-logging)
<!-- novolis-pkg-brand:end -->

# Novolis.Logging.Ndjson

Reusable structured `ILogger` output backed by a rotating NDJSON file sink.

The package owns logging-provider behavior: scopes, event ids, structured
state, rotation, retention, and durable flushes for error records. The
physical typed store remains in `Novolis.Storage.Ndjson`, while low-level
seekable viewing remains in `Novolis.IO.Ndjson`.

```csharp
builder.Logging.AddNdjsonLogging(options =>
{
    options.DirectoryPath = paths.RootDirectory;
    options.FilePrefix = "application";
});
```

One writer owner must be assigned to each path when processes or pods share a
filesystem. Readers use shared handles and can inspect a file while it grows.
