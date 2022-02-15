using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace OkTools.Core;

[PublicAPI]
public class EmptyDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> where TKey: notnull
{
    public static readonly IReadOnlyDictionary<TKey, TValue> Instance = new EmptyDictionary<TKey, TValue>();

    EmptyDictionary() {} // use `.Instance`

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => 0;
    public bool ContainsKey(TKey key) => false;
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) { value = default; return false; }
    public TValue this[TKey key] => throw new KeyNotFoundException();
    public IEnumerable<TKey> Keys => Enumerable.Empty<TKey>();
    public IEnumerable<TValue> Values => Enumerable.Empty<TValue>();
}

// TODO: defaultdict (don't derive Dictionary, copy-pasta it)

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
}
