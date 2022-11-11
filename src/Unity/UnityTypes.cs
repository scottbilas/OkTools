using System.Diagnostics;
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

    public bool IsValid => u64_0 != 0 || u64_1 != 0;

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
public unsafe struct UnityGUID
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

    public UnityGUID(uint a, uint b, uint c, uint d) { _data[0] = a; _data[1] = b; _data[2] = c; _data[3] = d; }

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

// IMPORTANT: these assume "this" is a pointer into blob memory. only arrive at these through other structs or
// casts of memory. never copy them.
//
// note that these are not `ref structs` because a) current c# restricts ref structs being used as generic type
// params (preventing BlobArray<BlobString> or some other type that contains a BlobOffsetPtr<x> or whatever) and b) it
// doesn't really solve the problem, since blob types can still be copied around on the stack, which still invalidates
// them.

public struct BlobOffsetPtr<T> where T : unmanaged
{
    public readonly int Offset;

    public unsafe T* Ptr
    {
        get
        {
            fixed (BlobOffsetPtr<T>* self = &this)
                return (T*)((byte*)self + Offset);
        }
    }

    public override unsafe string? ToString() => Ptr->ToString();
}

public struct BlobOptional<T> where T : unmanaged
{
    BlobOffsetPtr<T> _ptr;

    public bool HasValue => _ptr.Offset != 0;
    public unsafe T* Ptr => _ptr.Offset != 0 ? _ptr.Ptr : null;
    public override string? ToString() => _ptr.Offset != 0 ? _ptr.ToString() : null;
}

public struct BlobString
{
    BlobOffsetPtr<byte> _ptr;

    public unsafe void RefBytes(out byte* start, out byte* end)
    {
        start = end = _ptr.Ptr;
        while (*end != 0)
            ++end;
    }

    public override string ToString()
    {
        // this is intended to catch accidental use of ToString (like when passing into an interpolated string). it will
        // break because "this" changes during the copy/boxing and end up accessing invalid memory. Blob* types must
        // always remain anchored in their originating unmanaged memory.
        throw new InvalidOperationException("Use GetString instead");
    }

    public unsafe string GetString()
    {
        RefBytes(out var start, out var end);
        return Encoding.ASCII.GetString(start, (int)(end - start));
    }
}

public struct BlobArray<T> where T : unmanaged
{
    BlobOffsetPtr<T> _ptr;
    readonly uint _length; // count of elements

    public int Length => (int)_length;
    public bool Any => _length != 0;

    public unsafe T* PtrAt(int index) // don't make this an indexer because BlobArrays often used as a pointer and too easy to become ambiguous
    {
        Debug.Assert(index >= 0 && index < _length);
        return _ptr.Ptr + index;
    }

    public unsafe T[] ToArray()
    {
        var array = new T[_length];
        for (var i = 0; i < _length; ++i)
            array[i] = *PtrAt(i);
        return array;
    }
}
