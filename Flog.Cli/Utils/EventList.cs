class EventBuffer<T>
{
    readonly List<T> _buffer = new();
    readonly List<bool> _accepted = new();

    public void Add(T value)
    {
        _buffer.Add(value);
        _accepted.Add(false);
    }

    public void Clear()
    {
        _buffer.Clear();
        _accepted.Clear();
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator
    {
        readonly EventBuffer<T> _owner;
        int _index = -1;

        public Enumerator(EventBuffer<T> owner) => _owner = owner;
        public EnumeratorEntry Current => new(_owner, _index);

        public bool MoveNext()
        {
            for (;;)
            {
                if (++_index >= _owner._buffer.Count)
                    return false;
                if (!_owner._accepted[_index])
                    return true;
            }
        }
    }

    public readonly struct EnumeratorEntry
    {
        readonly EventBuffer<T> _owner;
        readonly int _index;

        public EnumeratorEntry(EventBuffer<T> owner, int index)
        {
            _owner = owner;
            _index = index;
        }

        public T Value => _owner._buffer[_index];

        public void Accept()
        {
            if (_owner._accepted[_index])
                throw new InvalidOperationException("Item has already been accepted");

            _owner._accepted[_index] = true;
        }
    }
}
