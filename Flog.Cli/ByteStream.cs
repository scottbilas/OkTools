class ByteStream
{
    byte[] _buffer = new byte[128];
    int _read, _write;

    public int Count => _write - _read;
    public bool IsEmpty => _write == _read;

    public void AddRange(ReadOnlyMemory<byte> memory)
    {
        var capacity = _buffer.Length;
        while (_write + memory.Length > capacity)
            capacity += capacity / 2;

        if (capacity != _buffer.Length)
            Array.Resize(ref _buffer, capacity);

        memory.Span.CopyTo(_buffer.AsSpan(_write));
        _write += memory.Length;
    }

    public void Reset()
    {
        _read = _write = 0;
    }

    void ThrowIfEmpty()
    {
        if (_read >= _write)
            throw new IndexOutOfRangeException("Stream is empty");
    }

    public byte Read()
    {
        ThrowIfEmpty();
        return _buffer[_read++];
    }

    public void Skip(int count = 1)
    {
        if ((_read + count) > _write)
            throw new IndexOutOfRangeException("Can't skip past end of stream");
        _read += count;
    }

    public byte Peek()
    {
        ThrowIfEmpty();
        return _buffer[_read];
    }

    public Span<byte> Span => new(_buffer, _read, Count);
}
