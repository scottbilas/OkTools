namespace OkTools.Core.Extensions;

public static partial class EnumerableExtensions
{
#   if !NET8_0_OR_GREATER
    public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<(TKey key, TValue value)> @this) where TKey: notnull =>
        @this.ToDictionary(item => item.key, item => item.value);
#   endif

    public static Dictionary<TKey, List<TValue>> ToMultiDictionary<TKey, TValue>(
        this IEnumerable<(TKey key, TValue value)> @this) where TKey: notnull
    {
        var dict = new Dictionary<TKey, List<TValue>>();
        foreach (var (key, value) in @this)
        {
            if (!dict.TryGetValue(key, out var list))
                dict.Add(key, list = []);
            list.Add(value);
        }
        return dict;
    }

    public static Dictionary<TKey, List<TValue>> ToMultiDictionary<TKey, TValue>(
        this IEnumerable<TValue> @this, Func<TValue, TKey> keySelector) where TKey: notnull
    {
        var dict = new Dictionary<TKey, List<TValue>>();
        foreach (var item in @this)
        {
            var key = keySelector(item);
            if (!dict.TryGetValue(key, out var list))
                dict.Add(key, list = []);
            list.Add(item);
        }
        return dict;
    }

    public static Dictionary<TKey, List<TValue>> ToMultiDictionary<TItem, TKey, TValue>(
        this IEnumerable<TItem> @this, Func<TItem, TKey> keySelector, Func<TItem, TValue> valueSelector) where TKey: notnull
    {
        var dict = new Dictionary<TKey, List<TValue>>();
        foreach (var item in @this)
        {
            var key = keySelector(item);
            if (!dict.TryGetValue(key, out var list))
                dict.Add(key, list = []);
            list.Add(valueSelector(item));
        }
        return dict;
    }

    public static IDictionary<TKey, List<TValue>> ToDefaultMultiDictionary<TKey, TValue>(
        this IEnumerable<(TKey key, TValue value)> @this) where TKey: notnull =>
            @this.ToMultiDictionary().ToDefaultDictionary();
    public static IDictionary<TKey, List<TValue>> ToDefaultMultiDictionary<TKey, TValue>(
        this IEnumerable<TValue> @this, Func<TValue, TKey> keySelector) where TKey: notnull =>
            @this.ToMultiDictionary(keySelector).ToDefaultDictionary();
    public static IDictionary<TKey, List<TValue>> ToDefaultMultiDictionary<TItem, TKey, TValue>(
        this IEnumerable<TItem> @this, Func<TItem, TKey> keySelector, Func<TItem, TValue> valueSelector) where TKey: notnull =>
            @this.ToMultiDictionary(keySelector, valueSelector).ToDefaultDictionary();

    public static Dictionary<TKey, List<TValue>> ToMultiDictionary<TKey, TValue>(
        this IEnumerable<IGrouping<TKey, TValue>> @this) where TKey: notnull =>
            @this.ToDictionary(g => g.Key, g => g.ToList());
    public static IDictionary<TKey, List<TValue>> ToDefaultMultiDictionary<TKey, TValue>(
        this IEnumerable<IGrouping<TKey, TValue>> @this) where TKey: notnull =>
            @this.ToMultiDictionary().ToDefaultDictionary();
}
