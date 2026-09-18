using System.Collections;

namespace Novolis.Logging.Contracts;

/// <summary>Merges nested <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope"/> dictionaries for text and wire sinks.</summary>
public static class LoggingScopeStack
{
    private static readonly AsyncLocal<Stack<Dictionary<string, string>>?> Scopes = new();

    public static IReadOnlyDictionary<string, string>? CurrentMerged
    {
        get
        {
            var stack = Scopes.Value;
            return stack is { Count: > 0 } ? stack.Peek() : null;
        }
    }

    internal static IDisposable Push<TState>(TState state) where TState : notnull
    {
        if (state is not IEnumerable<KeyValuePair<string, object>> kvps)
            return NullDisposable.Instance;

        var layer = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in kvps)
            layer[kv.Key] = kv.Value?.ToString() ?? "";

        var stack = Scopes.Value;
        if (stack == null)
        {
            stack = new Stack<Dictionary<string, string>>();
            Scopes.Value = stack;
        }

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        if (stack.Count > 0)
        {
            foreach (var kv in stack.Peek())
                merged[kv.Key] = kv.Value;
        }

        foreach (var kv in layer)
            merged[kv.Key] = kv.Value;

        stack.Push(merged);
        return new PopScope(stack);
    }

    private sealed class PopScope(Stack<Dictionary<string, string>> stack) : IDisposable
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
                Scopes.Value = null;
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        internal static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
