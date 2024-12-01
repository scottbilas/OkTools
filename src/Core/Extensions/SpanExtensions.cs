// DO NOT MODIFY, THIS FILE IS GENERATED

namespace OkTools.Core.Extensions;

[PublicAPI]
public static partial class ReadOnlySpanExtensions
{
    public static ReadOnlySpan<T> Slice<T>(this ReadOnlySpan<T> @this, int start) =>
        @this.Slice(start, @this.Length - start);

    public static ReadOnlySpan<T> SliceSafe<T>(this ReadOnlySpan<T> @this, int start, int length)
    {
        if (start < 0)
        {
            length -= -start;
            start = 0;
        }

        if (start >= @this.Length || length <= 0)
            return new();
        if (start + length >= @this.Length)
            return @this.Slice(start);

        return @this.Slice(start, length);
    }

    public static ReadOnlySpan<T> SliceSafe<T>(this ReadOnlySpan<T> @this, int start)
    {
        if (start < 0)
            return @this;
        if (start >= @this.Length)
            return new();

        return @this.Slice(start);
    }
}

[PublicAPI]
public static partial class SpanExtensions
{
    public static Span<T> Slice<T>(this Span<T> @this, int start) =>
        @this.Slice(start, @this.Length - start);

    public static Span<T> SliceSafe<T>(this Span<T> @this, int start, int length)
    {
        if (start < 0)
        {
            length -= -start;
            start = 0;
        }

        if (start >= @this.Length || length <= 0)
            return new();
        if (start + length >= @this.Length)
            return @this.Slice(start);

        return @this.Slice(start, length);
    }

    public static Span<T> SliceSafe<T>(this Span<T> @this, int start)
    {
        if (start < 0)
            return @this;
        if (start >= @this.Length)
            return new();

        return @this.Slice(start);
    }
}

[PublicAPI]
public static partial class StringSegmentExtensions
{
    public static StringSegment Slice(this StringSegment @this, int start) =>
        @this.Slice(start, @this.Length - start);

    public static StringSegment SliceSafe(this StringSegment @this, int start, int length)
    {
        if (start < 0)
        {
            length -= -start;
            start = 0;
        }

        if (start >= @this.Length || length <= 0)
            return new();
        if (start + length >= @this.Length)
            return @this.Slice(start);

        return @this.Slice(start, length);
    }

    public static StringSegment SliceSafe(this StringSegment @this, int start)
    {
        if (start < 0)
            return @this;
        if (start >= @this.Length)
            return new();

        return @this.Slice(start);
    }
}

