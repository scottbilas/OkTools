using System.Runtime.InteropServices;

#pragma warning disable CA1707
#pragma warning disable CA1720
// ReSharper disable InconsistentNaming

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
    fixed UInt32 data[4];

    public UnityGUID(uint a, uint b, uint c, uint d) { data[0] = a; data[1] = b; data[2] = c; data[3] = d; }

    static readonly char[] kHexToLiteral = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

    public override string ToString()
    {
        return string.Create(32, this, (chars, g) =>
        {
            // adapted from GUIDToString (Runtime/Utilities/UnityGUID.cpp)
            for (var i = 0; i < 4; ++i)
            {
                for (var j = 8; j-- > 0;)
                {
                    var cur = g.data[i];
                    cur >>= j * 4;
                    cur &= 0xF;
                    chars[i * 8 + j] = kHexToLiteral[cur];
                }
            }
        });
    }
}

[PublicAPI]
public struct ImporterID
{
    public Int32 nativeImporterType;
    public Hash128 scriptedImporterType;
}
