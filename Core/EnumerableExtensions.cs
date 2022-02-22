using System.Collections;

namespace OkTools.Core;

[PublicAPI]
public static class EnumerableExtensions
{
    // traits

    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? @this) =>
        @this == null || !@this.Any();

    public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T>? @this) =>
        @this ?? Enumerable.Empty<T>();

    public static IReadOnlyList<T> OrEmpty<T>(this IReadOnlyList<T>? @this) =>
        @this ?? Array.Empty<T>();

    // singles

    public static T SingleOr<T>(this IEnumerable<T> @this, T defaultValue)
    {
        using var e = @this.GetEnumerator();

        if (!e.MoveNext())
            return defaultValue;
        var value = e.Current;
        if (e.MoveNext())
            throw new InvalidOperationException("Sequence contains more than one element");

        return value;
    }

    public static T SingleOr<T>(this IEnumerable<T> @this, Func<T> defaultValueGenerator)
    {
        using var e = @this.GetEnumerator();

        if (!e.MoveNext())
            return defaultValueGenerator();
        var value = e.Current;
        if (e.MoveNext())
            throw new InvalidOperationException("Sequence contains more than one element");

        return value;
    }

    // filtering and searching

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> @this) where T: class =>
        @this.Where(item => item is not null)!;

    public static IEnumerable<TResult> SelectWhere<TSource, TResult>(
        this IEnumerable<TSource> @this,
        Func<TSource, (TResult selected, bool shouldSelect)> selectWhere)
    {
        foreach (var item in @this)
        {
            var (selected, shouldSelect) = selectWhere(item);
            if (shouldSelect)
                yield return selected;
        }
    }

    public static int IndexOf<T>(this IEnumerable<T> @this, Func<T, bool> predicate)
    {
        var index = 0;
        foreach (var item in @this)
        {
            if (predicate(item))
                return index;
            ++index;
        }
        return -1;
    }

    // stream alteration

    public static IEnumerable<T> SelectMany<T>(this IEnumerable<IEnumerable<T>> @this) =>
        @this.SelectMany(_ => _);

    public static IEnumerable<T> Flatten<T>(this IEnumerable @this)
    {
        foreach (var item in @this)
        {
            // IComparable items are probably primitives that also happen to be enumerable (like string), and we don't want to flatten them
            if (item is not IComparable && item is IEnumerable items)
            {
                foreach (var child in Flatten<T>(items))
                    yield return child;
            }
            else
                yield return (T)item;
        }
    }

    public static IEnumerable<object> Flatten(this IEnumerable @this) =>
        @this.Flatten<object>();

    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> @this) =>
        @this.Select((item, index) => (item, index));

    // copy to collection

    public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<(TKey key, TValue value)> @this) where TKey: notnull =>
        @this.ToDictionary(item => item.key, item => item.value);

    public static Queue<T> ToQueue<T>(this IEnumerable<T> @this) =>
        new(@this);

    // backing store

    public static IReadOnlyList<T> UnDefer<T>(this IEnumerable<T> @this) =>
        @this as IReadOnlyList<T> ?? @this.ToList(); // consider ToArray (do a perf test with modern .net)

    public static IList<T> EnsureList<T>(this IEnumerable<T> items) =>
        items as IList<T> ?? items.ToList();

    // ordering

    public static IOrderedEnumerable<T> Ordered<T>(this IEnumerable<T> @this) =>
        @this.OrderBy(_ => _);

    public static bool IsDistinct<T>(this IEnumerable<T> @this)
    {
        var seen = new HashSet<T>();
        return @this.All(item => seen.Add(item));
    }
}
