using System.Diagnostics.CodeAnalysis;

namespace OkTools.Core.Extensions;

[PublicAPI]
public static class DictionaryExtensions
{
    public static TValue? GetValueOr<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, TValue? defaultValue = default) =>
        @this.TryGetValue(key, out var value) ? value : defaultValue;

    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, Func<TKey, TValue> createFunc)
    {
        if (@this.TryGetValue(key, out var found))
            return found;

        found = createFunc(key);
        @this.Add(key, found);
        return found;
    }

    public static TValue? GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue?> @this, TKey key, TValue? defaultValue = default)
    {
        if (@this.TryGetValue(key, out var found))
            return found;

        @this.Add(key, defaultValue);
        return defaultValue;
    }

    public static IDictionary<TKey, TValue> AddRange<TKey, TValue>(this IDictionary<TKey, TValue> @this, IEnumerable<(TKey key, TValue value)> items)
    {
        foreach (var (key, value) in items)
            @this.Add(key, value);

        return @this;
    }

    public static IDictionary<TKey, TValue> AddRange<TKey, TValue>(this IDictionary<TKey, TValue> @this, IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        foreach (var (key, value) in items)
            @this.Add(key, value);

        return @this;
    }

    public static IDictionary<TKey, TValue> AddOrUpdateRange<TKey, TValue>(this IDictionary<TKey, TValue> @this, IEnumerable<(TKey key, TValue value)> items)
    {
        foreach (var (key, value) in items)
            @this[key] = value;

        return @this;
    }

    public static IDictionary<TKey, TValue> AddOrUpdateRange<TKey, TValue>(this IDictionary<TKey, TValue> @this, IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        foreach (var (key, value) in items)
            @this[key] = value;

        return @this;
    }

    public static bool TryGetValue<TValue>(this Dictionary<string, TValue> @this, ReadOnlySpan<char> key, [MaybeNullWhen(false)] out TValue value)
    {
#       if NET9_0_OR_GREATER
        return @this.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(key, out value);
#       else
        return @this.TryGetValue(key.ToString(), out value); // have to alloc :/
#       endif
    }
}
