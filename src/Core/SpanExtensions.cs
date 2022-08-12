namespace OkTools.Core;

public static class ReadOnlySpanExtensions
{
    public static ReadOnlySpan<T> SliceSafe<T>(this ReadOnlySpan<T> @this, int start, int length)
    {
        if (start < 0)
        {
            length += start;
            start = 0;
        }

        if (start + length > @this.Length)
            length = @this.Length - start;

        if (length <= 0)
            return default;

        return @this.Slice(start, length);
    }

    public static ReadOnlySpan<T> SliceSafe<T>(this ReadOnlySpan<T> @this, int start)
    {
        if (start < 0)
            start = 0;
        else if (start >= @this.Length)
            return default;

        return @this[start..];
    }
}

public static class SpanExtensions
{
    public static Span<T> SliceSafe<T>(this Span<T> @this, int start, int length)
    {
        if (start < 0)
        {
            length += start;
            start = 0;
        }

        if (start + length > @this.Length)
            length = @this.Length - start;

        if (length <= 0)
            return default;

        return @this.Slice(start, length);
    }

    public static Span<T> SliceSafe<T>(this Span<T> @this, int start)
    {
        if (start < 0)
            start = 0;
        else if (start >= @this.Length)
            return default;

        return @this[start..];
    }
}

