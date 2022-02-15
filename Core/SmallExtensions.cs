namespace OkTools.Core;

[PublicAPI]
public static class ObjectExtensions
{
    // fluent operators - note that we're limiting to ref types where needed to avoid accidental boxing

    public static T    ToBase<T>(this T @this) => @this; // inline upcast is sometimes convenient
    public static T    To    <T>(this object @this) where T: class => (T)@this;
    public static T?   As    <T>(this object @this) where T: class => @this as T;
    public static bool Is    <T>(this object @this) where T: class => @this is T;
    public static bool IsNot <T>(this object @this) where T: class => @this is not T;
}

[PublicAPI]
public static class RefTypeExtensions
{
    public static IEnumerable<T> WrapInEnumerable<T>(this T @this)
        { yield return @this; }

    public static IEnumerable<T> WrapInEnumerableOrEmpty<T>(this T? @this) where T: class =>
        ReferenceEquals(@this, null) ? Enumerable.Empty<T>() : WrapInEnumerable(@this);

    /// <summary>Return the result of `operation` on the given object if non-null, otherwise just return null</summary>
    public static T? OrNull<T>(this T? @this, Func<T, T> operation) where T: class =>
        @this != null ? operation(@this) : null;
}

[PublicAPI]
public static class ComparableExtensions
{
    /// <summary>Return the given value, clamped to [min, max]</summary>
    public static T Clamp<T>(this T @this, T min, T max) where T: IComparable<T>
    {
        if (min.CompareTo(max) > 0)
            throw new ArgumentException("'min' cannot be greater than 'max'", nameof(min));

        if (@this.CompareTo(min) < 0) return min;
        if (@this.CompareTo(max) > 0) return max;
        return @this;
    }
}

[PublicAPI]
public static class ByteArrayExtensions
{
    public static string ToHexString(this byte[] @this) =>
        Convert.ToHexString(@this);
}

[PublicAPI]
public static class ListExtensions
{
    public static void SetRange<T>(this List<T> @this, IEnumerable<T> collection)
    {
        @this.Clear();
        @this.AddRange(collection);
    }

    public static T PopBack<T>(this IList<T> @this)
    {
        var item = @this[^1];
        @this.DropBack();
        return item;
    }

    public static void DropBack<T>(this IList<T> @this)
    {
        @this.RemoveAt(@this.Count - 1);
    }
}
