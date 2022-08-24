namespace OkTools.ProcMonUtils;

class SimpleParserException : Exception
{
    public SimpleParserException(string message)
        : base(message) {}
}

struct SimpleParser
{
    public string Text;
    public int Offset;

    public SimpleParser(string text, int offset = 0)
    {
        Text = text;
        Offset = offset;
    }

    public override string ToString()
    {
        return AsSpan().ToString();
    }

    public int Remain => Text.Length - Offset;
    public bool AtEnd => Remain == 0;
    public void Advance(int count) => Offset += count;

    public ReadOnlySpan<char> AsSpan()
    {
        return Text.AsSpan(Offset);
    }

    public int Count(char find)
    {
        var count = 0;
        for (var i = Offset; i < Text.Length; ++i)
        {
            if (Text[i] == find)
                ++count;
        }
        return count;
    }

    public void Expect(char expect)
    {
        var got = Text[Offset++];
        if (got != expect)
            throw new SimpleParserException($"Expected {expect}, got {got} at offset {Offset-1} for line: {Text}");
    }

    public void Expect(string expect)
    {
        foreach (var c in expect)
        {
            var got = Text[Offset++];
            if (got != c)
                throw new SimpleParserException($"Expected {c}, got {got} at offset {Offset-1} for line: {Text}");
        }
    }

    public ReadOnlySpan<char> ReadStringUntil(char terminator)
    {
        var start = Offset;
        while (Text[Offset] != terminator)
            ++Offset;

        return Text.AsSpan(start, Offset - start);
    }

    public char ReadChar()
    {
        return Text[Offset++];
    }

    public char PeekChar()
    {
        return Text[Offset];
    }

    public char PeekCharSafe()
    {
        return Offset == Text.Length ? '\0' : Text[Offset];
    }

    public ulong ReadULong()
    {
        var i = 0ul;
        var start = Offset;

        while (Offset < Text.Length)
        {
            uint c = Text[Offset];
            if (c is < '0' or > '9')
                break;

            var old = i;
            i = 10*i + (c-'0');
            if (i < old)
                throw new OverflowException($"Integer starting at offset {start} too big in line: {Text}");

            ++Offset;
        }

        if (start == Offset)
            throw new SimpleParserException($"Expected uint, got {Text[start]} at offset {start} for line: {Text}");

        return i;
    }

    public ulong ReadULongHex()
    {
        var result = 0ul;
        var start = Offset;

        while (Offset < Text.Length)
        {
            uint c = Text[Offset];
            var old = result;

            if (c is >= '0' and <= '9')
                result = 16*result + (c-'0');
            else if (c is >= 'a' and <= 'f')
                result = 16*result + (c-'a'+10);
            else if (c is >= 'A' and <= 'F')
                result = 16*result + (c-'A'+10);
            else
                break;

            if (result < old)
                throw new OverflowException($"Integer starting at offset {start} too big in line: {Text}");

            ++Offset;
        }

        if (start == Offset)
        {
            throw Offset == Text.Length
                ? new SimpleParserException($"Expected ulong, got end of text for line: {Text}")
                : new SimpleParserException($"Expected uint, got {Text[Offset]} at offset {Offset} for line: {Text}");
        }

        return result;
    }

    /*  String.Join(", ", Enumerable.Range(0, 256).Select(i => i switch
        {
            >= '0' and <= '9' => i - '0',
            >= 'a' and <= 'f' => i - 'a' + 10,
            >= 'A' and <= 'F' => i - 'A' + 10,
            _ => -1
        })) */
    static readonly sbyte[] k_HexLut = {
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
        -1, -1, -1, -1, -1, -1, -1,
        10, 11, 12, 13, 14, 15,
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        10, 11, 12, 13, 14, 15,
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1, -1, -1, -1, -1 };

    // 30% faster than ReadULongHex../shrug..
    public unsafe ulong ReadULongHexUnsafe()
    {
        fixed (char* text = Text)
        fixed (sbyte* lut = k_HexLut)
        {
            var result = 0ul;

            var start = text + Offset; // utf8
            var str = start;

            for (;;) // internally in .net, strings are null-terminated, so no need to check against length
            {
                var digit = lut[(byte)*str]; // chop to byte to avoid needing bounds check
                if (digit < 0)
                    break;

                var old = result;
                result = 16*result + (ulong)digit;

                if (result < old)
                    throw new OverflowException($"Integer starting at offset {Offset} too big in line: {Text}");

                ++str;
            }

            Offset += (int)(str - start);

            if (start == str)
            {
                throw Offset == Text.Length
                    ? new SimpleParserException($"Expected ulong, got end of text for line: {Text}")
                    : new SimpleParserException($"Expected ulong, got {*str} at offset {Offset} for line: {Text}");
            }

            return result;
        }
    }
}
