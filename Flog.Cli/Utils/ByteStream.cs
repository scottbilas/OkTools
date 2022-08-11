class ByteStream
{
    byte[] _buffer = new byte[128];
    int _read, _write;

    public int Count => _write - _read;
    public bool IsEmpty => _write == _read;
    public Memory<byte> Memory => new(_buffer, _read, Count);
    public Span<byte> Span => new(_buffer, _read, Count);

    public void Reset()
    {
        _read = _write = 0;
    }

    public void Write(ReadOnlyMemory<byte> memory)
    {
        EnsureCanWrite(memory.Length);

        memory.Span.CopyTo(_buffer.AsSpan(_write));
        _write += memory.Length;
    }

    public byte Read()
    {
        ThrowIfEmpty();
        return _buffer[_read++];
    }

    public byte Peek()
    {
        ThrowIfEmpty();
        return _buffer[_read];
    }

    public void Seek(int offset = 1)
    {
        if ((_read + offset) > _write)
            throw new ArgumentOutOfRangeException(nameof(offset), "Can't skip past end of stream");
        _read += offset;
    }

    void ThrowIfEmpty()
    {
        if (_read >= _write)
            throw new OverflowException("Stream is empty");
    }

    void EnsureCanWrite(int count)
    {
        var capacity = _buffer.Length;
        while (_write + count > capacity)
            capacity += capacity / 2;

        if (capacity != _buffer.Length)
            Array.Resize(ref _buffer, capacity);
    }
}
