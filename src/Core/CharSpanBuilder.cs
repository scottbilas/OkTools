namespace OkTools.Core;

// TODO: look at ArrayBufferWriter and ArrayPool<char>.Shared(.Rent)
public ref struct CharSpanBuilder
{
    readonly char[] _buffer;
    Span<char> _used;

    public CharSpanBuilder(char[] buffer)
    {
        _buffer = buffer;
        _used = default;
    }

    public CharSpanBuilder(int capacity)
    {
        _buffer = new char[capacity];
        _used = default;
    }

    public override string ToString() => new(Span);
    public static implicit operator ReadOnlySpan<char>(in CharSpanBuilder @this) => @this._used;

    public int Length
    {
        get => _used.Length;
        set => _used = _buffer.AsSpan(0, value);
    }

    public readonly int UnusedLength => _buffer.Length - _used.Length;

    public void Clear() => _used = default;

    public Span<char> Span => _used;
    public ArraySegment<char> Chars => new(_buffer, 0, _used.Length); // for older API's that can't take spans and require char[]
    public Span<char> UnusedSpan => _buffer.AsSpan(_used.Length);

    public bool TryAppend(int value)
    {
        if (!value.TryFormat(UnusedSpan, out var used))
            return false;
        Use(used);
        return true;
    }

    public void Append(int value)
    {
        if (!TryAppend(value))
            ThrowInsufficientSpace(value);
    }

    public bool TryAppend(ReadOnlySpan<char> value)
    {
        if (!value.TryCopyTo(UnusedSpan))
            return false;
        Use(value.Length);
        return true;
    }

    public void Append(ReadOnlySpan<char> value)
    {
        if (!TryAppend(value))
            ThrowInsufficientSpace(new string(value));
    }

    public int AppendTrunc(ReadOnlySpan<char> value)
    {
        var count = Math.Min(value.Length, UnusedLength);
        if (count != 0)
        {
            value[..count].CopyTo(UnusedSpan);
            Use(count);
        }
        return count;
    }

    public bool TryAppend(char value)
    {
        if ( _used.Length >= _buffer.Length)
            return false;
        _buffer[_used.Length] = value;
        Use(1);
        return true;
    }

    public void Append(char value)
    {
        if (!TryAppend(value))
            ThrowInsufficientSpace(value);
    }

    public bool TryAppend(char value, int count)
    {
        if (count == 0)
            return true;
        if ( _used.Length + count > _buffer.Length)
            return false;
        Array.Fill(_buffer, value, _used.Length, count);
        Use(count);
        return true;

    }

    public void Append(char value, int count)
    {
        if (!TryAppend(value, count))
            ThrowInsufficientSpace(value);
    }

    public void FillTo(char value, int width)
    {
        Append(value, Math.Max(0, width - Length));
    }

    void Use(int count)
    {
        _used = _buffer.AsSpan(0, _used.Length + count);
    }

    void ThrowInsufficientSpace<T>(T value)
    {
        throw new OverflowException($"Insufficient space to store '{value}' ({UnusedSpan.Length} remain)");
    }
}
