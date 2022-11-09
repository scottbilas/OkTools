using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable BuiltInTypeReferenceStyle
// ReSharper disable InconsistentNaming
#pragma warning disable CS0649
#pragma warning disable CA1707
#pragma warning disable CA1720
#pragma warning disable IDE0044
#pragma warning disable IDE0052

namespace UnityEngine;

[PublicAPI]
public struct Hash128
{
    public const int SizeOf = sizeof(ulong)*2;
    UInt64 u64_0, u64_1;

    public Hash128(uint u32_0, uint u32_1, uint u32_2, uint u32_3)
    {
        u64_0 = ((ulong)u32_1) << 32 | u32_0;
        u64_1 = ((ulong)u32_3) << 32 | u32_2;
    }

    public Hash128(ulong u64_0, ulong u64_1)
    {
        this.u64_0 = u64_0;
        this.u64_1 = u64_1;
    }

    public override string ToString()
    {
        // TODO: overload that writes direct to a span
        var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref this, 1));
        return bytes.ToHexString();
    }
}

[PublicAPI]
public unsafe struct UnityGuid
{
    // Be aware that because of endianness and because we store a GUID as four integers instead
    // of as the DWORD, WORD, and BYTE groupings as used by Microsoft, the individual bytes
    // may not end up where you expect them to be.  In particular both GUID and UUID specify Data4
    // (which is data[2] and data[3] here) as big endian but we split this value into two DWORDSs
    // both of which we store as little endian.  So, if you're looking for the type bits in the GUID,
    // they are found in the least significant byte of data[2] instead of in the most significant
    // byte (where you would expect them).
    // Also, our text format neither conforms to the canonical format for GUIDs nor for UUIDs so
    // again the bits change place here (the group of type bits is found one character to the right).
    public const int SizeOf = sizeof(uint)*4;
    fixed UInt32 _data[4];

    public UnityGuid(uint a, uint b, uint c, uint d) { _data[0] = a; _data[1] = b; _data[2] = c; _data[3] = d; }

    static readonly char[] k_kHexToLiteral = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

    public override string ToString()
    {
        // TODO: overload that writes direct to a span
        return string.Create(32, this, (chars, g) =>
        {
            // adapted from GUIDToString (Runtime/Utilities/UnityGUID.cpp)
            for (var i = 0; i < 4; ++i)
            {
                for (var j = 8; j-- > 0;)
                {
                    var cur = g._data[i];
                    cur >>= j * 4;
                    cur &= 0xF;
                    chars[i * 8 + j] = k_kHexToLiteral[cur];
                }
            }
        });
    }
}

struct BlobString
{
    readonly int _offset; // start of string characters as an offset from "this"

    public unsafe ReadOnlySpan<byte> RefBytes()
    {
        fixed (BlobString* self = &this)
        {
            var start = (byte*)self + _offset;

            var end = start;
            while (*end != 0)
                ++end;

            return new ReadOnlySpan<byte>(start, (int)(end - start));
        }
    }

    public override string ToString() =>
        Encoding.ASCII.GetString(RefBytes());
}

struct BlobArray<T> where T : unmanaged
{
    readonly int _offset;
    readonly uint _size; // count of elements

    public int Length => (int)_size;

    public unsafe T* RefElementFromBlob(int index)
    {
        fixed (BlobArray<T>* self = &this)
        {
            var origin = (T*)((byte*)self + _offset);
            return origin + index;
        }
    }

    public unsafe T[] ToArrayFromBlob()
    {
        fixed (BlobArray<T>* self = &this)
        {
            var origin = (T*)((byte*)self + _offset);

            var array = new T[_size];
            for (var i = 0; i < _size; ++i)
                array[i] = origin[i];

            return array;
        }
    }
}
