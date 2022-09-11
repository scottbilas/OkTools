using System.Collections;

namespace OkTools.Core;

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

    // TODO: i don't love this "fixed" naming, the term can be ambiguous.

    /// <summary>
    /// This is a "fixed" version of `BitArray.RightShift`. The thing pretends to be an array (enumeration, indexing)
    /// but underneath it's integers, and their "shift" lets this implementation detail bleed through. Arrays go left
    /// to right, and that's the "fixed" naming here.
    ///
    /// Other notes:
    ///
    /// * The underlying implementation will always fill with undefined values (not necessarily 0's); we aren't given a choice. So we have to patch it up.
    /// * We throw if count > Length, to match `T[].ShiftLeft`.
    /// </summary>
    public static void ShiftLeftFixed(this BitArray @this, int count)
    {
        if ((uint)count > (uint)@this.Length)
            throw new ArgumentOutOfRangeException(nameof(count), $"Out of range 0 <= {count} <= {@this.Length}");
        @this.RightShift(count);
    }

    /// <summary>
    /// This is a "fixed" version of `BitArray.LeftShift`. The thing pretends to be an array (enumeration, indexing)
    /// but underneath it's integers, and their "shift" lets this implementation detail bleed through. Arrays go left
    /// to right, and that's the "fixed" naming here.
    ///
    /// Other notes:
    ///
    /// * The underlying implementation will always fill with 0's, we aren't given a choice.
    /// * We throw if count > Length, to match `T[].ShiftRight`.
    /// </summary>
    public static void ShiftRightFixed(this BitArray @this, int count)
    {
        if ((uint)count > (uint)@this.Length)
            throw new ArgumentOutOfRangeException(nameof(count), $"Out of range 0 <= {count} <= {@this.Length}");
        @this.LeftShift(count);
    }

    // this will always fill with 0 (Right/LeftShift do not give us a choice)
    public static void Shift(this BitArray @this, int count)
    {
        if (count > 0)
            @this.ShiftRightFixed(count);
        else
            @this.ShiftLeftFixed(-count);
    }

    // really for debugging only, this is not fast
    public static bool[] ToArray(this BitArray @this)
    {
        var bits = new bool[@this.Length];
        @this.CopyTo(bits, 0);
        return bits;
    }
}
