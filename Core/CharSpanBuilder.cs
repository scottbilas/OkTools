namespace OkTools.Core;

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

    public int Length
    {
        get => _used.Length;
        set => _used = _buffer.AsSpan(0, value);
    }

    public void Clear() => _used = default;

    public (char[] chars, int used) Chars => (_buffer, _used.Length); // for older api's that don't take spans but can use part of an array (e.g. Console.Write*)
    public ReadOnlySpan<char> Span => _used;
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

    public bool TryAppend(string value)
    {
        if (!value.TryCopyTo(UnusedSpan))
            return false;
        Use(value.Length);
        return true;
    }

    public void Append(string value)
    {
        if (!TryAppend(value))
            ThrowInsufficientSpace(value);
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

    void Use(int count)
    {
        _used = _buffer[..(_used.Length + count)];
    }

    void ThrowInsufficientSpace<T>(T value)
    {
        throw new Exception($"Insufficient space to store '{value}' ({UnusedSpan.Length} remain)");
    }
}
