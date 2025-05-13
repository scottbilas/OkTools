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

    // this IDictionary<TKey, TValue>
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue> @this, Func<TKey, TValue> getDefValue) where TKey: notnull =>
            new DefaultDictionary<TKey, TValue>(getDefValue, @this);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue> @this, Func<TValue> getDefValue) where TKey: notnull =>
            new DefaultDictionary<TKey, TValue>(getDefValue, @this);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue> @this, TValue defValue) where TKey: notnull =>
            new DefaultDictionary<TKey, TValue>(defValue, @this);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue> @this) where TKey: notnull where TValue: struct =>
            new DefaultDictionary<TKey, TValue>(default(TValue), @this);

    // this IEnumerable<KeyValuePair<TKey, TValue>>
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> @this, Func<TKey, TValue> getDefValue) where TKey: notnull =>
        new DefaultDictionary<TKey, TValue>(getDefValue, @this);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> @this, Func<TValue> getDefValue) where TKey: notnull =>
        new DefaultDictionary<TKey, TValue>(getDefValue, @this);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> @this, TValue defValue) where TKey: notnull =>
        new DefaultDictionary<TKey, TValue>(defValue, @this);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> @this) where TKey: notnull where TValue: struct =>
        new DefaultDictionary<TKey, TValue>(default(TValue), @this);

    // this IEnumerable<(TKey, TValue)>
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<(TKey, TValue)> @this, Func<TKey, TValue> getDefValue) where TKey: notnull =>
        new DefaultDictionary<TKey, TValue>(getDefValue, @this);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<(TKey, TValue)> @this, Func<TValue> getDefValue) where TKey: notnull =>
        new DefaultDictionary<TKey, TValue>(getDefValue, @this);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<(TKey, TValue)> @this, TValue defValue) where TKey: notnull =>
        new DefaultDictionary<TKey, TValue>(defValue, @this);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<(TKey, TValue)> @this) where TKey: notnull where TValue: struct =>
        new DefaultDictionary<TKey, TValue>(default(TValue), @this);

    // this IEnumerable<TSource>
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TSource, TKey, TValue>(
        this IEnumerable<TSource> @this, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector, Func<TKey, TValue> getDefValue) where TKey: notnull =>
        @this.Select(v => new KeyValuePair<TKey, TValue>(keySelector(v), valueSelector(v))).ToDefaultDictionary(getDefValue);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<TValue> @this, Func<TValue, TKey> keySelector, Func<TKey, TValue> getDefValue) where TKey: notnull =>
        @this.Select(v => new KeyValuePair<TKey, TValue>(keySelector(v), v)).ToDefaultDictionary(getDefValue);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TSource, TKey, TValue>(
        this IEnumerable<TSource> @this, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector, Func<TValue> getDefValue) where TKey: notnull =>
        @this.Select(v => new KeyValuePair<TKey, TValue>(keySelector(v), valueSelector(v))).ToDefaultDictionary(getDefValue);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<TValue> @this, Func<TValue, TKey> keySelector, Func<TValue> getDefValue) where TKey: notnull =>
        @this.Select(v => new KeyValuePair<TKey, TValue>(keySelector(v), v)).ToDefaultDictionary(getDefValue);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TSource, TKey, TValue>(
        this IEnumerable<TSource> @this, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector, TValue defValue) where TKey: notnull =>
        @this.Select(v => new KeyValuePair<TKey, TValue>(keySelector(v), valueSelector(v))).ToDefaultDictionary(defValue);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<TValue> @this, Func<TValue, TKey> keySelector, TValue defValue) where TKey: notnull =>
        @this.Select(v => new KeyValuePair<TKey, TValue>(keySelector(v), v)).ToDefaultDictionary(defValue);
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TSource, TKey, TValue>(
        this IEnumerable<TSource> @this, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector) where TKey: notnull where TValue: struct =>
        @this.Select(v => new KeyValuePair<TKey, TValue>(keySelector(v), valueSelector(v))).ToDefaultDictionary();
    public static IDictionary<TKey, TValue> ToDefaultDictionary<TKey, TValue>(
        this IEnumerable<TValue> @this, Func<TValue, TKey> keySelector) where TKey: notnull where TValue: struct =>
        @this.Select(v => new KeyValuePair<TKey, TValue>(keySelector(v), v)).ToDefaultDictionary();

    public static IDictionary<TKey, List<TValue>> ToDefaultDictionary<TKey, TValue>(
        this IDictionary<TKey, List<TValue>> @this) where TKey: notnull =>
        @this.ToDefaultDictionary(_ => []);
}
