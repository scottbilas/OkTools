using System.Buffers;
using System.Diagnostics;

namespace OkTools.Core;

// TODO: consider deleting this class in favor of System.IO.Pipelines.Pipe (move useful methods from here to extensions on Pipe)
[PublicAPI]
public class StreamableBuffer<T> : IBufferWriter<T> where T : struct
{
    T[] _buffer;
    int _read, _write;

    public StreamableBuffer(int capacity = 128)
        { _buffer = new T[capacity]; }

    // terminology:
    //
    // * writable: how much space is available to write to without a realloc (write functions that take a size or items will realloc on demand)
    // * unread: how much data has been written that is available to read

    public int TotalCapacity => _buffer.Length;

    public bool HasUnread => _write != _read;
    public int UnreadCount => _write - _read;
    public Memory<T> UnreadMemory => new(_buffer, _read, UnreadCount);
    public Span<T> UnreadSpan => new(_buffer, _read, UnreadCount);

    public Span<T> GetUnreadSpan(int size)
        { ThrowIfUnreadLessThan(size); return new(_buffer, _read, size); }
    public Memory<T> GetUnreadMemory(int size)
        { ThrowIfUnreadLessThan(size); return new(_buffer, _read, size); }

    public bool HasWritable => _write != _buffer.Length;
    public int WritableCount => _buffer.Length - _write;
    public Memory<T> WritableMemory => new(_buffer, _write, WritableCount);
    public Span<T> WritableSpan => new(_buffer, _write, WritableCount);

    public Span<T> GetWritableSpan(int size)
    {
        if (WritableCount < size)
            ExpandByAtLeast(size);
        return new(_buffer, _write, size);
    }

    public Memory<T> GetWritableMemory(int size)
    {
        if (WritableCount < size)
            ExpandByAtLeast(size);
        return new(_buffer, _write, size);
    }

    public void Reset()
    {
        _read = _write = 0;
    }

    public void Reset(int capacity)
    {
        Reset();
        if (TotalCapacity != capacity)
            _buffer = new T[capacity];
    }

    public void Write(ReadOnlyMemory<T> memory)
    {
        memory.Span.CopyTo(GetWritableSpan(memory.Length));
        _write += memory.Length;
    }

    public void Write(T value)
    {
        if (WritableCount < 1)
            ExpandByAtLeast(1);
        _buffer[_write++] = value;
    }

    public void AdvanceWriter(int count)
    {
        if (WritableCount < count)
            throw new InvalidOperationException("Can't advance writer past the end");
        _write += count;
    }

    public T Read()
        { ThrowIfUnreadLessThan(); return _buffer[_read++]; }
    public T Peek()
        { ThrowIfUnreadLessThan(); return _buffer[_read]; }
    public T TryPeek(T defValue) =>
        HasUnread ? _buffer[_read] : defValue;
    public T? TryPeek() =>
        HasUnread ? _buffer[_read] : null;

    public void SeekReader(int offset)
    {
        var newOffset = _read + offset;
        if (newOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Can't rewind past the beginning");
        if (newOffset > _write)
            throw new ArgumentOutOfRangeException(nameof(offset), "Can't skip past the end");
        _read += offset;
    }

    public void AdvanceReader()
    {
        if (_read == _write)
            throw new InvalidOperationException("Can't advance reader past the end");
        ++_read;
    }

    void ThrowIfUnreadLessThan(int count = 1)
    {
        if (UnreadCount < count)
            throw new OverflowException($"Insufficient data to read, need {count} but have {UnreadCount}");
    }

    void ExpandByAtLeast(int count)
    {
        var newCapacity = _buffer.Length;
        while (_write + count > newCapacity)
            newCapacity += Math.Max(newCapacity / 2, 1);

        Debug.Assert(newCapacity != _buffer.Length);
        Array.Resize(ref _buffer, newCapacity);
    }

    // TODO: change these from explicit to public, and align the rest of the API with it.
    // TODO: also consider implementing Stream (possibly via an AsStream() given the API is old and may not fit well)

    void IBufferWriter<T>.Advance(int count) => SeekReader(count);
    Memory<T> IBufferWriter<T>.GetMemory(int sizeHint) => GetWritableMemory(sizeHint);
    Span<T> IBufferWriter<T>.GetSpan(int sizeHint) => GetWritableSpan(sizeHint);
}
