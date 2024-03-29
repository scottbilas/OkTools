﻿using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.Diagnostics.Debug;

namespace OkTools.Core;

// like a List<T>, but simpler. also gives access to underlying T[] as a Memory<T>.
[PublicAPI]
[DebuggerDisplay("count:{Count}, capacity:{Capacity}")]
public class OkList<T> : IReadOnlyList<T>
{
    T[] _items;
    int _used;

    // note that capacity is always required; user must consider memory usage patterns when using this container

    public OkList(int capacity)
    {
        _items = new T[capacity];
    }

    // TODO: not loving this API but i want a way to use the same value for capacity and count (without having to do a OkList.FromCount<T> type thing..)
    public OkList(int? capacity, int count)
        : this(capacity ?? count)
    {
        if ((uint)count > (uint)Capacity)
            throw new ArgumentOutOfRangeException(nameof(count), $"Out of range 0 <= {count} <= {Capacity} (capacity)");

        _used = count;
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

            if (value <= Capacity)
            {
                Array.Clear(_items, _used, value - _used); // whether ref or not, all these newly-used elements need to be set to default
                _used = value;
                return;
            }

            // no need to do an Array.Clear because this will always get a new array (with unused elements filled with defaults)
            GrowToAtLeast(value);
            _used = value;
        }
    }

    public bool IsEmpty => _used == 0;
    public bool Any => _used != 0;

    void ReduceCountTo(int count)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(_items, count, _used - count); // gc will reclaim these
        _used = count;
    }

    public void SetCountDirect(int count)
    {
        if ((uint)count > (uint)Capacity)
            throw new ArgumentOutOfRangeException(nameof(count), $"Out of range 0 <= {count} <= {Capacity} (capacity)");

        _used = count;
    }

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

    public void FillDefault()
    {
        Array.Fill(_items, default, 0, _used);
    }

    public void Fill(in T value)
    {
        Array.Fill(_items, value, 0, _used);
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < _used; ++i)
            yield return _items[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.Length;

        set
        {
            if (value < _used)
                throw new ArgumentOutOfRangeException(nameof(value), $"Out of range {_used} (count) <= {value}");

            if (value != Capacity)
                SetCapacity(value);
        }
    }

    // try to reduce capacity to a reasonable number based on the number of used items and a minimum.
    // (note that this will never _increase_ capacity - "minimum" is just used to prevent the used*1.5 heuristic going too far.)
    public void TrimCapacity(int minimum)
    {
        var newCapacity = _used + _used / 2;
        if (newCapacity < minimum)
            newCapacity = minimum;
        if (newCapacity < Capacity)
            SetCapacity(newCapacity);
    }

    T IReadOnlyList<T>.this[int index] => this[index];

    public ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_used)
                throw new ArgumentOutOfRangeException(nameof(index), $"Out of range 0 <= {index} < {_used} (count)");
            return ref _items[index];
        }
    }

    public Span<T> Slice(int start, int length) => AsSpan.Slice(start, length);

    public Span<T> AsSpan => _items.AsSpan(0, _used);
    public Memory<T> AsMemory => new(_items, 0, _used);
    public ArraySegment<T> AsArraySegment => new(_items, 0, _used);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if (_used == Capacity)
            GrowToAtLeast(Capacity + 1);
        _items[_used++] = item;
    }

    public void AddRange(ReadOnlySpan<T> items)
    {
        var want = _used + items.Length;
        if (want > Capacity)
            GrowToAtLeast(want);
        items.CopyTo(_items.AsSpan(_used));
        _used += items.Length;
    }

    public void AddRange(params T[] items)
    {
        AddRange(items.AsSpan());
    }

    public void RemoveAtAndSwapBack(int index)
    {
        if ((uint)index >= (uint)_used)
            throw new ArgumentOutOfRangeException(nameof(index), $"Out of range 0 <= {index} < {_used} (count)");

        _items[index] = _items[_used - 1];
        ReduceCountTo(_used - 1);
    }

    public void DropBack()
    {
        if (_used == 0)
            throw new InvalidOperationException("Collection cannot be empty");

        ReduceCountTo(_used - 1);
    }

    public T PopBack()
    {
        if (_used == 0)
            throw new InvalidOperationException("Collection cannot be empty");

        var item = _items[_used - 1];
        ReduceCountTo(_used - 1);
        return item;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void GrowToAtLeast(int minCapacity)
    {
        Assert(minCapacity > Capacity);

        SetCapacity(Math.Max(minCapacity, Capacity + Capacity / 2));
    }

    void SetCapacity(int capacity)
    {
        var newItems = new T[capacity];
        Array.Copy(_items, newItems, _used);
        _items = newItems;
    }
}
