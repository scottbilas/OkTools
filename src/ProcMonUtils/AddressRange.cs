﻿using System.Diagnostics.CodeAnalysis;

namespace OkTools.ProcMonUtils;

public interface IAddressRange
{
    ref readonly AddressRange AddressRef { get; }
}

public readonly struct AddressRange
{
    public readonly ulong Base;
    public readonly uint Size;
    public ulong End => Base + Size;

    public AddressRange(ulong @base, uint size)
    {
        Base = @base;
        Size = size;
    }
}

public static class AddressRangeExtensions
{
    public static bool TryFindAddressIn<T>(this T[] items, ulong address, [NotNullWhen(returnValue: true)] out T? result)
        where T : IAddressRange
    {
        if (items.Length == 0 || address < items[0].AddressRef.Base || address >= items[^1].AddressRef.End)
        {
            result = default;
            return false;
        }

        for (ulong l = 0, h = (ulong)(items.Length - 1); l <= h; )
        {
            var i = l + (h - l) / 2;
            var test = items[i];

            if (test.AddressRef.End <= address)
                l = i + 1;
            else if (test.AddressRef.Base > address)
                h = i - 1;
            else
            {
                result = test;
                return true;
            }
        }

        result = default!;
        return false;
    }
}