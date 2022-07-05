using System.Collections;
using System.Runtime.CompilerServices;
using static System.Diagnostics.Debug;

namespace OkTools.Core;

// like a List<T>, but simpler. also gives access to underlying T[] as a Memory<T>.
[PublicAPI]
public class OkList<T> : IReadOnlyList<T>
{
    T[] _items;
    int _used;

    public OkList(int capacity)
    {
        _items = new T[capacity];
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _used;

        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot be negative");

            if (value <= _used)
            {
                ReduceCountTo(value);
                return;
            }

            if (value <= _items.Length)
            {
                Array.Clear(_items, _used, value - _used); // whether ref or not, all these newly-used elements need to be set to default
                _used = value;
                return;
            }

            // no need to do an Array.Clear because this will always get a new array (filled with defaults)
            GrowToAtLeast(value);
            _used = value;
        }
    }

    void ReduceCountTo(int count)
    {
        Assert(count >= 0 && count <= _used);

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(_items, count, _used - count); // gc will reclaim these
        _used = count;
    }

    public void SetCountDirect(int count)
    {
        if ((uint)count > (uint)_items.Length)
            throw new ArgumentOutOfRangeException(nameof(count), $"Out of range 0 <= {count} <= {Capacity} (capacity)");

        _used = count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        ReduceCountTo(0);
    }

    public void Clear(int trimCapacityTo)
    {
        if (trimCapacityTo >= Capacity)
        {
            Clear();
            return;
        }

        // no need to clear old array because we're throwing the whole thing out
        _items = new T[trimCapacityTo];
        _used = 0;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.Length;
    }

    // try to reduce capacity to a reasonable number based on the number of used items and a minimum.
    // (note that this will never _increase_ capacity - "minimum" is just used to prevent the used*1.5 heuristic going too far.)
    public void TrimCapacity(int minimum)
    {
        var capacity = _used + _used / 2;
        if (capacity < minimum)
            capacity = minimum;
        if (capacity < Capacity)
            SetCapacity(capacity);
    }

    T IReadOnlyList<T>.this[int index] => this[index];

    public ref T this[int index]
    {
        get
        {
            if (index >= _used)
                throw new IndexOutOfRangeException();
            return ref _items[index];
        }
    }

    public ArraySegment<T> AsArraySegment => new(_items, 0, _used);
    public Span<T> AsSpan => _items.AsSpan(0, _used);
    public Memory<T> AsMemory => new(_items, 0, _used);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if (_used == _items.Length)
            GrowToAtLeast(_items.Length + 1);
        _items[_used++] = item;
    }

    public void AddRange(ReadOnlySpan<T> items)
    {
        var want = _used + items.Length;
        if (want > _items.Length)
            GrowToAtLeast(want);
        items.CopyTo(_items.AsSpan(_used));
        _used += items.Length;
    }

    public void AddRange(params T[] items)
    {
        AddRange(items.AsSpan());
    }

    void GrowToAtLeast(int minCapacity)
    {
        Assert(minCapacity > _items.Length);

        SetCapacity(Math.Max(minCapacity, _items.Length + _items.Length / 2));
    }

    void SetCapacity(int capacity)
    {
        var newItems = new T[capacity];
        Array.Copy(_items, newItems, _used);
        _items = newItems;
    }
}
