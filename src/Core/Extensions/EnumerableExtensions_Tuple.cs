namespace OkTools.Core.Extensions;

public static partial class EnumerableExtensions
{
    public static (T a, T b) ToTuple2<T>(this IEnumerable<T> @this)
    {
        using var e = @this.GetEnumerator();

        e.MoveNext();
        var t0 = e.Current;
        e.MoveNext();
        var t1 = e.Current;

        var tuple = ValueTuple.Create(t0, t1);
        if (e.MoveNext())
            throw new InvalidOperationException("More than 2 elements found");
        return tuple;
    }

    public static IEnumerable<(T a, T b)> SelectTuple2<T>(this IEnumerable<T> @this)
    {
        using var e = @this.GetEnumerator();
        while (e.MoveNext())
        {
            var a = e.Current;
            e.MoveNext();
            yield return (a, e.Current);
        }
    }

    public static (T a, T b, T c) ToTuple3<T>(this IEnumerable<T> @this)
    {
        using var e = @this.GetEnumerator();

        e.MoveNext();
        var t0 = e.Current;
        e.MoveNext();
        var t1 = e.Current;
        e.MoveNext();
        var t2 = e.Current;

        var tuple = ValueTuple.Create(t0, t1, t2);
        if (e.MoveNext())
            throw new InvalidOperationException("More than 3 elements found");
        return tuple;
    }

    public static IEnumerable<(T a, T b, T c)> SelectTuple3<T>(this IEnumerable<T> @this)
    {
        using var e = @this.GetEnumerator();
        while (e.MoveNext())
        {
            var a = e.Current;
            e.MoveNext();
            var b = e.Current;
            e.MoveNext();
            yield return (a, b, e.Current);
        }
    }

    public static (T a, T b, T c, T d) ToTuple4<T>(this IEnumerable<T> @this)
    {
        using var e = @this.GetEnumerator();

        e.MoveNext();
        var t0 = e.Current;
        e.MoveNext();
        var t1 = e.Current;
        e.MoveNext();
        var t2 = e.Current;
        e.MoveNext();
        var t3 = e.Current;

        var tuple = ValueTuple.Create(t0, t1, t2, t3);
        if (e.MoveNext())
            throw new InvalidOperationException("More than 4 elements found");
        return tuple;
    }

    public static IEnumerable<(T a, T b, T c, T d)> SelectTuple4<T>(this IEnumerable<T> @this)
    {
        using var e = @this.GetEnumerator();
        while (e.MoveNext())
        {
            var a = e.Current;
            e.MoveNext();
            var b = e.Current;
            e.MoveNext();
            var c = e.Current;
            e.MoveNext();
            yield return (a, b, c, e.Current);
        }
    }
}
