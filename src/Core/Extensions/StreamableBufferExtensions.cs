using System.Buffers;

namespace OkTools.Core.Extensions;

public static class StreamableBufferExtensions
{
    // TODO: consider moving this to *SpanExtensions. except lose the nicer exception from GetUnreadSpan

    public static void Write<T>(this StreamableBuffer<T> @this, ReadOnlySequence<T> sequence) where T : struct
    {
        foreach (var segment in sequence)
            @this.Write(segment);
    }

    public static (T a, T b) Peek2<T>(this StreamableBuffer<T> @this) where T : struct
    {
        var span = @this.GetUnreadSpan(2);
        return (span[0], span[1]);
    }

    public static (T a, T b, T c) Peek3<T>(this StreamableBuffer<T> @this) where T : struct
    {
        var span = @this.GetUnreadSpan(3);
        return (span[0], span[1], span[2]);
    }

    // TODO: yeah these could go into span extensions

    public static (T a, T b) TryPeek2<T>(this StreamableBuffer<T> @this, T defValue) where T : struct
    {
        var span = @this.UnreadSpan;
        return span.Length switch
        {
            0 => (defValue, defValue),
            1 => (span[0],  defValue),
            _ => (span[0],   span[1])
        };
    }

    public static (T a, T b, T c) TryPeek3<T>(this StreamableBuffer<T> @this, T defValue) where T : struct
    {
        var span = @this.UnreadSpan;
        return span.Length switch
        {
            0 => (defValue, defValue, defValue),
            1 => (span[0],  defValue, defValue),
            2 => (span[0],   span[1], defValue),
            _ => (span[0],   span[1],  span[2])
        };
    }

    public static (T a, T b)? TryPeek2<T>(this StreamableBuffer<T> @this) where T : struct
    {
        var span = @this.UnreadSpan;
        if (span.Length >= 2)
            return (span[0], span[1]);
        return null;
    }

    public static (T a, T b, T c)? TryPeek3<T>(this StreamableBuffer<T> @this) where T : struct
    {
        var span = @this.UnreadSpan;
        if (span.Length >= 3)
            return (span[0], span[1], span[2]);
        return null;
    }

    // helpful for parsing
    public static bool AdvanceIfNextEquals<T>(StreamableBuffer<T> @this, T value)
        where T : struct, IEquatable<T>
    {
        var unread = @this.UnreadSpan;
        if (unread.Length == 0 || !value.Equals(unread[0]))
            return false;

        @this.AdvanceReader();
        return true;
    }
}
