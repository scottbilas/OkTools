namespace OkTools.Core.Extensions;

public static partial class EnumerableExtensions
{
    public static IEnumerable<T> SelectItems<T>(this (T a, T b) @this)
    {
        yield return @this.a;
        yield return @this.b;
    }

    public static IEnumerable<T> SelectItems<T>(this (T a, T b, T c) @this)
    {
        yield return @this.a;
        yield return @this.b;
        yield return @this.c;
    }

    public static IEnumerable<T> SelectItems<T>(this (T a, T b, T c, T d) @this)
    {
        yield return @this.a;
        yield return @this.b;
        yield return @this.c;
        yield return @this.d;
    }

    public static IEnumerable<T> SelectItems<T>(this (T a, T b, T c, T d, T e) @this)
    {
        yield return @this.a;
        yield return @this.b;
        yield return @this.c;
        yield return @this.d;
        yield return @this.e;
    }
}
