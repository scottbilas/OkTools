namespace OkTools.Core.Extensions;

public static partial class EnumerableExtensions
{
    public static (T a, T b) ToTuple2<T>(this IEnumerable<T> @this)
    {
        using var iter = @this.GetEnumerator();

        iter.MoveNext();
        var t0 = iter.Current;
        iter.MoveNext();
        var t1 = iter.Current;

        var tuple = ValueTuple.Create(t0, t1);
        if (iter.MoveNext())
            throw new InvalidOperationException("More than 2 elements found");
        return tuple;
    }

    public static IEnumerable<(T a, T b)> SelectTuple2<T>(this IEnumerable<T> @this)
    {
        using var iter = @this.GetEnumerator();
        while (iter.MoveNext())
        {
            var a = iter.Current;
            iter.MoveNext();
            yield return (a, iter.Current);
        }
    }

    public static IEnumerable<T> SelectMany<T>(this IEnumerable<(T a, T b)> @this)
    {
        foreach (var (a, b) in @this)
        {
            yield return a;
            yield return b;
        }
    }

    public static (T a, T b, T c) ToTuple3<T>(this IEnumerable<T> @this)
    {
        using var iter = @this.GetEnumerator();

        iter.MoveNext();
        var t0 = iter.Current;
        iter.MoveNext();
        var t1 = iter.Current;
        iter.MoveNext();
        var t2 = iter.Current;

        var tuple = ValueTuple.Create(t0, t1, t2);
        if (iter.MoveNext())
            throw new InvalidOperationException("More than 3 elements found");
        return tuple;
    }

    public static IEnumerable<(T a, T b, T c)> SelectTuple3<T>(this IEnumerable<T> @this)
    {
        using var iter = @this.GetEnumerator();
        while (iter.MoveNext())
        {
            var a = iter.Current;
            iter.MoveNext();
            var b = iter.Current;
            iter.MoveNext();
            yield return (a, b, iter.Current);
        }
    }

    public static IEnumerable<T> SelectMany<T>(this IEnumerable<(T a, T b, T c)> @this)
    {
        foreach (var (a, b, c) in @this)
        {
            yield return a;
            yield return b;
            yield return c;
        }
    }

    public static (T a, T b, T c, T d) ToTuple4<T>(this IEnumerable<T> @this)
    {
        using var iter = @this.GetEnumerator();

        iter.MoveNext();
        var t0 = iter.Current;
        iter.MoveNext();
        var t1 = iter.Current;
        iter.MoveNext();
        var t2 = iter.Current;
        iter.MoveNext();
        var t3 = iter.Current;

        var tuple = ValueTuple.Create(t0, t1, t2, t3);
        if (iter.MoveNext())
            throw new InvalidOperationException("More than 4 elements found");
        return tuple;
    }

    public static IEnumerable<(T a, T b, T c, T d)> SelectTuple4<T>(this IEnumerable<T> @this)
    {
        using var iter = @this.GetEnumerator();
        while (iter.MoveNext())
        {
            var a = iter.Current;
            iter.MoveNext();
            var b = iter.Current;
            iter.MoveNext();
            var c = iter.Current;
            iter.MoveNext();
            yield return (a, b, c, iter.Current);
        }
    }

    public static IEnumerable<T> SelectMany<T>(this IEnumerable<(T a, T b, T c, T d)> @this)
    {
        foreach (var (a, b, c, d) in @this)
        {
            yield return a;
            yield return b;
            yield return c;
            yield return d;
        }
    }

    public static IEnumerable<(T a, T b, T c, T d, T e)> SelectTuple5<T>(this IEnumerable<T> @this)
    {
        using var iter = @this.GetEnumerator();
        while (iter.MoveNext())
        {
            var a = iter.Current;
            iter.MoveNext();
            var b = iter.Current;
            iter.MoveNext();
            var c = iter.Current;
            iter.MoveNext();
            var d = iter.Current;
            iter.MoveNext();
            yield return (a, b, c, d, iter.Current);
        }
    }

    public static IEnumerable<(T a, T b, T c, T d, T e, T f)> SelectTuple6<T>(this IEnumerable<T> @this)
    {
        using var iter = @this.GetEnumerator();
        while (iter.MoveNext())
        {
            var a = iter.Current;
            iter.MoveNext();
            var b = iter.Current;
            iter.MoveNext();
            var c = iter.Current;
            iter.MoveNext();
            var d = iter.Current;
            iter.MoveNext();
            var e = iter.Current;
            iter.MoveNext();
            yield return (a, b, c, d, e, iter.Current);
        }
    }

    public static IEnumerable<T> SelectMany<T>(this IEnumerable<(T a, T b, T c, T d, T e)> @this)
    {
        foreach (var (a, b, c, d, e) in @this)
        {
            yield return a;
            yield return b;
            yield return c;
            yield return d;
            yield return e;
        }
    }
}
