namespace OkTools.Core;

public static class ArrayExtensions
{
    public static ArraySegment<T> WithOffset<T>(this T[] @this, int offset) =>
        new(@this, offset, @this.Length - offset);
    public static ArraySegment<T> WithLength<T>(this T[] @this, int length) =>
        new(@this, 0, length);
    public static ArraySegment<T> Slice<T>(this T[] @this, int offset, int length) =>
        new(@this, offset, length);

    public static void ShiftLeft<T>(this T[] @this, int count)
    {
        Array.Copy(@this, count, @this, 0, @this.Length - count);
    }

    public static void ShiftLeft<T>(this T[] @this, int count, T fill)
    {
        @this.ShiftLeft(count);
        Array.Fill(@this, fill, @this.Length - count, count);
    }

    public static void ShiftRight<T>(this T[] @this, int count)
    {
        Array.Copy(@this, 0, @this, count, @this.Length - count);
    }

    public static void ShiftRight<T>(this T[] @this, int count, T fill)
    {
        @this.ShiftRight(count);
        Array.Fill(@this, fill, 0, count);
    }

    public static void Shift<T>(this T[] @this, int count)
    {
        if (count > 0)
            @this.ShiftRight(count);
        else
            @this.ShiftLeft(-count);
    }

    public static void Shift<T>(this T[] @this, int count, T fill)
    {
        if (count > 0)
            @this.ShiftRight(count, fill);
        else
            @this.ShiftLeft(-count, fill);
    }
}
