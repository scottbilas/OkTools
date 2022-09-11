namespace OkTools.Core;

// TODO: look at ArrayBufferWriter and ArrayPool<char>.Shared(.Rent)
public ref struct CharSpanBuilder
{
    readonly Span<char> _buffer;
    readonly char[]? _array; // null if ctor only given a span to work with
    Span<char> _used;

    public CharSpanBuilder(Span<char> buffer) => _buffer = buffer;
    public CharSpanBuilder(char[] array) => _buffer = _array = array;
    public CharSpanBuilder(int capacity) => _buffer = _array = new char[capacity];

    public override string ToString() => new(Span);
    public static implicit operator ReadOnlySpan<char>(in CharSpanBuilder @this) => @this._used;

    public int Length
    {
        get => _used.Length;
        set => _used = _buffer[..value];
    }

    public readonly int UnusedLength => _buffer.Length - _used.Length;

    public void Clear() => _used = default;

    public Span<char> Span => _used;
    public Span<char> UnusedSpan => _buffer[_used.Length..];

    public ArraySegment<char> Chars => _array != null
        ? new(_array, 0, _used.Length)
        : throw new InvalidOperationException("Span-only CharSpanBuilder cannot implicitly convert to Chars"); // for older API's that can't take spans and require char[]

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
        var use = Math.Min(value.Length, UnusedLength);
        if (use != 0)
        {
            value[..use].CopyTo(UnusedSpan);
            Use(use);
        }
        return use;
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
        _buffer.Slice(_used.Length, count).Fill(value);
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
        _used = _buffer[..(_used.Length + count)];
    }

    void ThrowInsufficientSpace<T>(T value)
    {
        throw new OverflowException($"Insufficient space to store '{value}' ({UnusedSpan.Length} remain)");
    }
}
