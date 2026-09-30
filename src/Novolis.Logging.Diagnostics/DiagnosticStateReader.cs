using Microsoft.Extensions.Logging;

namespace Novolis.Logging.Diagnostics;

/// <summary>Reads named values out of an <see cref="ILogger"/> state object.</summary>
internal static class DiagnosticStateReader
{
    public static Dictionary<string, object?>? Read(object? state)
    {
        if (state is not System.Collections.IEnumerable enumerable)
            return null;

        Dictionary<string, object?>? values = null;
        foreach (var item in enumerable)
        {
            if (item is not KeyValuePair<string, object> pair)
                continue;
            if (pair.Key.Length == 0 || pair.Key == "{OriginalFormat}")
                continue;

            values ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            values[Name(pair.Key)] = pair.Value;
        }

        return values;
    }

    public static Dictionary<string, object?> Merge(
        IReadOnlyDictionary<string, object?>? scope,
        IReadOnlyDictionary<string, object?>? properties)
    {
        var merged = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (scope is not null)
        {
            foreach (var pair in scope)
                merged[pair.Key] = pair.Value;
        }

        if (properties is not null)
        {
            foreach (var pair in properties)
                merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private static string Name(string key)
    {
        if (key.Contains('.', StringComparison.Ordinal))
            return key;
        if (key.Length == 0 || !char.IsUpper(key[0]))
            return key;
        return char.ToLowerInvariant(key[0]) + key[1..];
    }
}
