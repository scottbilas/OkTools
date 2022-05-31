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

    public (char[] chars, int used) Chars => (_buffer, _used.Length);
    public ReadOnlySpan<char> Span => _used;
    public void Clear() => _used = default;

    Span<char> Unused => _buffer.AsSpan(_used.Length);

    void Use(int count)
    {
        _used = _buffer[..(_used.Length + count)];
    }

    public void Append(int value)
    {
        var success = value.TryFormat(Unused, out var used);
        if (!success)
            throw new Exception($"Insufficient space to store '{value}' ({Unused.Length} remain)");
        Use(used);
    }

    public void Append(string value)
    {
        var success = value.TryCopyTo(Unused);
        if (!success)
            throw new Exception($"Insufficient space to store '{value}' ({Unused.Length} remain)");
        Use(value.Length);
    }

    public void Append(char value)
    {
        var success = _used.Length < _buffer.Length;
        if (!success)
            throw new Exception($"Insufficient space to store '{value}' ({Unused.Length} remain)");
        _buffer[_used.Length] = value;
        Use(1);
    }
}
