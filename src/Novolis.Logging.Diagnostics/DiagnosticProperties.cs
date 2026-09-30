namespace Novolis.Logging.Diagnostics;

/// <summary>Named values attached to one diagnostic event.</summary>
public sealed class DiagnosticProperties
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public DiagnosticProperties Set(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _values[name] = Normalize(value);
        return this;
    }

    internal Dictionary<string, object?> Snapshot() =>
        new(_values, StringComparer.Ordinal);

    private static object? Normalize(object? value) => value switch
    {
        null => null,
        string or bool or int or long or double or float or decimal => value,
        _ => value.ToString(),
    };
}
