using System.Collections;
using System.Runtime.CompilerServices;

[PublicAPI]
class OkList<T> : IReadOnlyList<T>
{
    T[] _items;
    int _used;

    public OkList(int capacity)
    {
        _items = new T[capacity];
    }

    public int Count
    {
        get => _used;

        set
        {
            if (value <= _used)
            {
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                    Array.Clear(_items, value, _used - value); // gc will reclaim these
                _used = value;
                return;
            }

            if (value <= _items.Length)
            {
                Array.Clear(_items, _used, value - _used); // whether ref or not, all these newly-used elements need to be set to default
                _used = value;
                return;
            }

            // no need to do an Array.Clear because this will always get a new array (filled with defaults)
            GrowByAtLeast(value - _used);
            _used = value;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < _used; ++i)
            yield return _items[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Capacity
    {
        set
        {
            if (value < _used || value == 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (value != _items.Length)
                SetCapacity(value);
        }
        get => _items.Length;
    }

    public void TrimCapacity()
    {
        SetCapacity(_used + _used/ 2);
    }

    public void TrimCapacityExact()
    {
        SetCapacity(_used);
    }

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_used)
                throw new IndexOutOfRangeException();
            return _items[index];
        }

        set
        {
            if ((uint)index >= (uint)_used)
                throw new IndexOutOfRangeException();
            _items[index] = value;
        }
    }

    public ref T RefAt(int index)
    {
        if ((uint)index >= (uint)_used)
            throw new IndexOutOfRangeException();
        return ref _items[index];
    }

    public ref T RefAtUnchecked(int index) => ref _items[index];

    public Span<T> AsSpan => _items.AsSpan(0, _used);
    public OkSegment<T> AsSegment => new(_items, 0, _used);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if (_used >= _items.Length)
            GrowByAtLeast(1);
        _items[_used++] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearCount()
    {
        _used = 0;
    }

    public void ClearItems()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(_items, 0, _used); // gc will reclaim these
        _used = 0;
    }

    public void ClearItems(int trimCapacityTo)
    {
        if (_items.Length > trimCapacityTo)
        {
            // no need to clear old array because we're throwing the whole thing out
            _items = new T[trimCapacityTo];
            _used = 0;
        }
        else
            ClearItems();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void GrowByAtLeast(int count)
    {
        var newCapacity = _items.Length + _items.Length / 2;
        var minCapacity = _used + count;

        if (newCapacity < minCapacity)
            newCapacity = minCapacity;

        SetCapacity(newCapacity);
    }

    void SetCapacity(int capacity)
    {
        var newItems = new T[capacity];
        Array.Copy(_items, newItems, _used);
        _items = newItems;
    }
}
