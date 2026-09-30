using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Diagnostics;

/// <summary>Writes a diagnostic event with a short message and named state.</summary>
public static class DiagnosticLog
{
    private static readonly AsyncLocal<string?> CurrentOperation = new();

    public static void Information(
        ILogger? logger,
        string message,
        DiagnosticProperties? properties = null) =>
        Write(logger, LogLevel.Information, message, properties, exception: null);

    public static void Error(
        ILogger? logger,
        Exception? exception,
        string message,
        DiagnosticProperties? properties = null) =>
        Write(logger, LogLevel.Error, message, properties, exception);

    /// <summary>
    /// Starts an application operation. Nested calls keep the outer id so the
    /// whole path shares <c>operation.id</c>.
    /// </summary>
    public static IDisposable BeginOperation(ILogger? logger)
    {
        if (CurrentOperation.Value is not null)
            return EmptyDisposable.Instance;

        var operationId = Guid.NewGuid().ToString("N");
        CurrentOperation.Value = operationId;
        var scope = logger?.BeginScope(new Dictionary<string, object>
        {
            ["operation.id"] = operationId,
        });
        return new OperationScope(scope);
    }

    private static void Write(
        ILogger? logger,
        LogLevel level,
        string message,
        DiagnosticProperties? properties,
        Exception? exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (logger is null)
            return;

        var evt = new DiagnosticEvent(message, properties?.Snapshot());
        logger.Log(level, 0, evt, exception, static (state, _) => state.Message);
    }

    private sealed class OperationScope(IDisposable? scope) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            scope?.Dispose();
            CurrentOperation.Value = null;
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        internal static readonly EmptyDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
