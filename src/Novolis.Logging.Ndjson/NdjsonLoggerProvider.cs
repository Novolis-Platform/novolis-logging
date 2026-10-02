using System.Collections;
using Microsoft.Extensions.Logging;
using Novolis.Logging.Contracts;

namespace Novolis.Logging.Ndjson;

/// <summary>Dedicated <see cref="ILoggerProvider"/> for structured NDJSON records.</summary>
[ProviderAlias("NovolisNdjson")]
public sealed class NdjsonLoggerProvider : ILoggerProvider
{
    private readonly INdjsonLogSink _sink;
    private readonly bool _ownsSink;
    private readonly AsyncLocal<Stack<Dictionary<string, string>>?> _scopes = new();
    private int _minimumLevel;
    private int _disposed;

    /// <summary>Creates a provider over an externally owned sink.</summary>
    public NdjsonLoggerProvider(
        INdjsonLogSink sink,
        LogLevel minimumLevel = LogLevel.Trace)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _minimumLevel = (int)minimumLevel;
    }

    /// <summary>Creates a provider with an owned rotating file sink.</summary>
    public NdjsonLoggerProvider(
        NdjsonFileSinkOptions options,
        LogLevel minimumLevel = LogLevel.Trace)
        : this(new NdjsonFileLogSink(options), minimumLevel, ownsSink: true)
    {
    }

    private NdjsonLoggerProvider(
        INdjsonLogSink sink,
        LogLevel minimumLevel,
        bool ownsSink)
        : this(sink, minimumLevel)
    {
        _ownsSink = ownsSink;
    }

    /// <summary>Minimum level accepted by this provider.</summary>
    public LogLevel MinimumLevel
    {
        get => (LogLevel)Volatile.Read(ref _minimumLevel);
        set => Volatile.Write(ref _minimumLevel, (int)value);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        return new NdjsonLogger(this, categoryName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_ownsSink && _sink is IDisposable disposable)
            disposable.Dispose();
        GC.SuppressFinalize(this);
    }

    private IDisposable PushScope<TState>(TState state)
        where TState : notnull
    {
        var values = ReadStringValues(state);
        if (values is null || values.Count == 0)
            return EmptyScope.Instance;

        var stack = _scopes.Value ?? new Stack<Dictionary<string, string>>();
        _scopes.Value = stack;
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        if (stack.Count > 0)
        {
            foreach (var pair in stack.Peek())
                merged[pair.Key] = pair.Value;
        }

        foreach (var pair in values)
            merged[pair.Key] = pair.Value;
        stack.Push(merged);
        return new ScopePopper(stack, _scopes);
    }

    private IReadOnlyDictionary<string, string>? CurrentScope =>
        _scopes.Value is { Count: > 0 } stack
            ? stack.Peek()
            : null;

    private sealed class NdjsonLogger(
        NdjsonLoggerProvider provider,
        string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            provider.PushScope(state);

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None
            && logLevel >= provider.MinimumLevel
            && Volatile.Read(ref provider._disposed) == 0;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            ArgumentNullException.ThrowIfNull(formatter);
            try
            {
                var structured = state as INdjsonStructuredState;
                var message = structured?.Message ?? formatter(state, exception);
                var values = MergeValues(
                    provider.CommonScope,
                    provider.CurrentScope,
                    structured?.Properties ?? ReadObjectValues(state));

                provider._sink.Append(
                    new NdjsonLogRecord
                    {
                        Time = DateTimeOffset.UtcNow,
                        Level = logLevel.ToString(),
                        Category = category,
                        Message = message,
                        EventId = eventId.Id == 0 ? null : eventId.Id,
                        EventName = string.IsNullOrWhiteSpace(eventId.Name) ? null : eventId.Name,
                        Exception = exception?.ToString(),
                        State = values,
                    },
                    flushToDisk: logLevel >= LogLevel.Error);
            }
            catch
            {
                // Logging must not replace the application's original operation.
            }
        }
    }

    private IReadOnlyDictionary<string, string>? CommonScope =>
        LoggingScopeStack.CurrentMerged;

    private static Dictionary<string, object?>? MergeValues(
        IReadOnlyDictionary<string, string>? commonScope,
        IReadOnlyDictionary<string, string>? localScope,
        IReadOnlyDictionary<string, object?>? state)
    {
        if (commonScope is null && localScope is null && (state is null || state.Count == 0))
            return null;

        var merged = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (commonScope is not null)
        {
            foreach (var pair in commonScope)
                merged[pair.Key] = pair.Value;
        }

        if (localScope is not null)
        {
            foreach (var pair in localScope)
                merged[pair.Key] = pair.Value;
        }

        if (state is not null)
        {
            foreach (var pair in state)
                merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private static Dictionary<string, object?>? ReadObjectValues(object? state)
    {
        if (state is not IEnumerable enumerable)
            return null;

        Dictionary<string, object?>? values = null;
        foreach (var item in enumerable)
        {
            if (item is not KeyValuePair<string, object> pair
                || pair.Key.Length == 0
                || pair.Key == "{OriginalFormat}")
            {
                continue;
            }

            values ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            values[pair.Key] = pair.Value;
        }

        return values;
    }

    private static Dictionary<string, string>? ReadStringValues<TState>(TState state)
        where TState : notnull
    {
        if (state is not IEnumerable enumerable)
            return null;

        Dictionary<string, string>? values = null;
        foreach (var item in enumerable)
        {
            if (item is not KeyValuePair<string, object> pair
                || pair.Key.Length == 0
                || pair.Key == "{OriginalFormat}")
            {
                continue;
            }

            values ??= new Dictionary<string, string>(StringComparer.Ordinal);
            values[pair.Key] = pair.Value?.ToString() ?? string.Empty;
        }

        return values;
    }

    private sealed class ScopePopper(
        Stack<Dictionary<string, string>> stack,
        AsyncLocal<Stack<Dictionary<string, string>>?> owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (stack.Count > 0)
                stack.Pop();
            if (stack.Count == 0)
                owner.Value = null;
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        internal static readonly EmptyScope Instance = new();

        public void Dispose()
        {
        }
    }
}
