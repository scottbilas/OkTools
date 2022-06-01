ref struct CharSpanBuilder
{
    readonly char[] _buffer;
    Span<char> _used;

    public CharSpanBuilder(int capacity)
    {
        _buffer = new char[capacity];
        _used = default;
    }

    public override string ToString() => new(Span);

    public int Length
    {
        get => _used.Length;
        set => _used = _buffer.AsSpan(0, value);
    }

    public (char[] chars, int used) Chars => (_buffer, _used.Length);
    public ReadOnlySpan<char> Span => _used;
    public void Clear() => _used = default;

    Span<char> Unused => _buffer.AsSpan(_used.Length);

    void Use(int count)
    {
        _used = _buffer[..(_used.Length + count)];
    }

    public bool TryAppend(int value)
    {
        if (!value.TryFormat(Unused, out var used))
            return false;
        Use(used);
        return true;
    }

    public void Append(int value)
    {
        if (!TryAppend(value))
            throw new Exception($"Insufficient space to store '{value}' ({Unused.Length} remain)");
    }

    public bool TryAppend(string value)
    {
        if (!value.TryCopyTo(Unused))
            return false;
        Use(value.Length);
        return true;
    }

    public void Append(string value)
    {
        if (!TryAppend(value))
            throw new Exception($"Insufficient space to store '{value}' ({Unused.Length} remain)");
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
            throw new Exception($"Insufficient space to store '{value}' ({Unused.Length} remain)");
    }
}
