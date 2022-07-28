public static class ArrayExtensions
{
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
