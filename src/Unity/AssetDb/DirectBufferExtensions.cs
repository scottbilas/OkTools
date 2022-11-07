using System.Runtime.CompilerServices;
using System.Text;
using Spreads.Buffers;

namespace OkTools.Unity.AssetDb;

[PublicAPI]
public static class DirectBufferExtensions
{
    public static unsafe T* Cast<T>(this in DirectBuffer @this) where T : unmanaged
    {
        return (T*)@this.Data;
    }

    public static T Read<T>(this ref DirectBuffer @this)
    {
        var rc = @this.Read<T>(0);
        @this = @this.Slice(Unsafe.SizeOf<T>());
        return rc;
    }

    // the "expect" functions are about trying to catch when the schema changes and structs are bigger than we expect

    public static T ReadExpectEnd<T>(this in DirectBuffer @this)
    {
        if (@this.Length != Unsafe.SizeOf<T>())
            throw new InvalidOperationException("Did not consume entire value");

        return @this.Read<T>(0);
    }

    public static void ExpectEnd(this in DirectBuffer @this)
    {
        if (!@this.IsEmpty)
            throw new InvalidOperationException("Did not consume entire value");
    }

    public static void ReadAscii(this ref DirectBuffer @this, StringBuilder dst, int len, bool hasNullTerm)
    {
        var strlen = len;
        if (hasNullTerm)
            --strlen;

        var span = @this.Span;
        for (var i = 0; i < strlen; ++i)
        {
            var b = span[i];
            if (b >= 128)
                throw new InvalidDataException("Unexpected non-ASCII character"); // TODO: test what happens when we have asset paths using unicode chars

            dst.Append((char)b);
        }

        @this = @this.Slice(len);
    }

    public static string ReadAscii(this ref DirectBuffer @this, int len, bool hasNullTerm)
    {
        var strlen = len;
        if (hasNullTerm)
            --strlen;

        var str = Encoding.ASCII.GetString(@this.Span[..strlen]);
        @this = @this.Slice(len);
        return str;
    }

    public static string ReadAscii(this ref DirectBuffer @this, bool hasNullTerm)
    {
        return @this.ReadAscii(@this.Length, hasNullTerm);
    }
}
