using System.Collections;
using System.Runtime.CompilerServices;
using static System.Diagnostics.Debug;

namespace OkTools.Core;

// like a OkList<T>, but double-ended. it does this using a ring buffer, so up to two spans are needed to efficiently access the underlying storage.
[PublicAPI]
public class OkDeList<T> : IReadOnlyList<T>
{
    T[] _items;
    int _head, _used;

    // microbenchmarking on .net 6:
    //
    //|   Method |      Mean | Code Size | Allocated |
    //|--------- |----------:|----------:|----------:|
    //| Baseline |  3.340 us |     155 B |     456 B | no wrapping
    //|   Modulo | 15.986 us |     168 B |     456 B | wrap using %
    //|       If |  6.818 us |     171 B |     456 B | [winner] wrap using comparison and subtract

    // note that capacity is always required; user must consider memory usage patterns when using this container

    public OkDeList(int capacity)
    {
        _items = new T[capacity];
    }

    public OkDeList(int? capacity, int count)
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
                BackReduceCountTo(value);
                return;
            }

            if (value <= Capacity)
            {
                var unused = UnusedSpans;
                var clear = value - _used;
                _used = value;

                // whether ref or not, all these newly-used elements need to be set to default

                var clear0 = Math.Min(clear, unused.Span0.Length);
                unused.Span0[..clear0].Clear();
                var clear1 = clear - clear0;
                unused.Span1[..clear1].Clear();

                return;
            }

            // no need to do an Array.Clear because this will always get a new array (with unused elements filled with defaults)
            GrowToAtLeast(value, 0);
            _used = value;
        }
    }

    public bool IsEmpty => _used == 0;
    public bool Any => _used != 0;

    void BackReduceCountTo(int count)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            var spans = AsSpans;
            var clear = _used - count;

            // gc will reclaim these
            var clear1 = Math.Min(clear, spans.Span1.Length);
            spans.Span1[^clear1..].Clear();
            var clear0 = clear - clear1;
            spans.Span0[^clear0..].Clear();
        }

        _used = count;
    }

    #if NO // keep this around in case I ever add a Count removal that operates on the front
    void FrontReduceCountTo(int count)
    {
        var clear = _used - count;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            var spans = AsSpans;

            // gc will reclaim these
            var clear0 = Math.Min(clear, spans.Span0.Length);
            spans.Span0[..clear0].Clear();
            var clear1 = clear - clear0;
            spans.Span1[..clear1].Clear();
        }

        _head += clear;
        if (_head >= Capacity)
            _head -= Capacity;

        _used = count;
    }
    #endif

    public void SetCountDirect(int count)
    {
        if ((uint)count > (uint)Capacity)
            throw new ArgumentOutOfRangeException(nameof(count), $"Out of range 0 <= {count} <= {Capacity} (capacity)");

        _used = count;
    }

    public void Clear()
    {
        BackReduceCountTo(0);
        _head = 0;
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
        _head = 0;
    }

    public void FillDefault()
    {
        var spans = AsSpans;
        spans.Span0.Fill(default!);
        spans.Span1.Fill(default!);
    }

    public void Fill(in T value)
    {
        var spans = AsSpans;
        spans.Span0.Fill(value);
        spans.Span1.Fill(value);
    }

    public IEnumerator<T> GetEnumerator()
    {
        // use OkDeList.GetEnumerator when doing IEnumerable things
        // use OkDeList.GetEnumerator.AsSpans when wanting to iterate over the data with minimal overhead

        var memories = AsMemories;

        for (var i = 0; i < memories.mem0.Length; i++)
            yield return memories.mem0.Span[i];
        for (var i = 0; i < memories.mem1.Length; i++)
            yield return memories.mem1.Span[i];
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
                Realloc(value, 0);
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
            Realloc(newCapacity, 0);
    }

    T IReadOnlyList<T>.this[int index] => this[index];

    public ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_used)
                throw new ArgumentOutOfRangeException(nameof(index), $"Out of range 0 <= {index} < {_used} (count)");

            return ref RefAtUnchecked(index);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int AdjustUnchecked(int index)
    {
        var adjusted = _head + index;
        var capacity = Capacity;

        if (adjusted >= capacity)
            adjusted -= capacity;

        return adjusted;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ref T RefAtUnchecked(int index)
    {
        return ref _items[AdjustUnchecked(index)];
    }

    public SpanPair<T> AsSpans
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var itemsSpan = _items.AsSpan();

            var wrapLen = _used - (Capacity - _head);
            if (wrapLen < 0)
                return new SpanPair<T>(itemsSpan.Slice(_head, _used));

            return new SpanPair<T>(
                itemsSpan[_head..],
                itemsSpan[..wrapLen]);
        }
    }

    internal SpanPair<T> UnusedSpans
    {
        get
        {
            var itemsSpan = _items.AsSpan();

            var wrapLen = _used - (Capacity - _head);
            if (wrapLen >= 0)
                return new SpanPair<T>(itemsSpan[wrapLen.._head]);

            return new SpanPair<T>(
                itemsSpan[(_head + _used)..],
                itemsSpan[.._head]);
        }
    }

    public (Memory<T> mem0, Memory<T> mem1) AsMemories
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var maxSegLen = Capacity - _head;

            if (_used <= maxSegLen)
                return (new(_items, _head, _used), default);

            return (
                new(_items, _head, maxSegLen),
                new(_items, 0, _used - maxSegLen));
        }
    }

    public (ArraySegment<T> seg0, ArraySegment<T> seg1) AsArraySegments
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var maxSegLen = Capacity - _head;

            if (_used <= maxSegLen)
                return (new(_items, _head, _used), default);

            return (
                new(_items, _head, maxSegLen),
                new(_items, 0, _used - maxSegLen));
        }
    }

    public void Add(T item)
    {
        if (_used == Capacity)
        {
            GrowToAtLeast(Capacity + 1, 0);
            _items[_used++] = item; // freshly packed; no need for offset/wrap
            return;
        }

        RefAtUnchecked(_used++) = item;
    }

    public void AddFront(T item)
    {
        if (_used == Capacity)
        {
            GrowToAtLeast(Capacity + 1, 1);
            _items[0] = item;
            _head = 0;
        }
        else if (--_head < 0)
            _head += Capacity;

        ++_used;
        _items[_head] = item;
    }

    public void AddRange(ReadOnlySpan<T> items)
    {
        var want = _used + items.Length;
        if (want > Capacity)
        {
            GrowToAtLeast(want, 0);
            items.CopyTo(_items.AsSpan()[_used..]);
            _used += items.Length;
            return;
        }

        var itemsSpan = _items.AsSpan();
        var start = _head + _used;
        _used += items.Length;

        if (start >= Capacity)
        {
            // open space starts after wrap
            start -= Capacity;
            Assert(start + items.Length <= _head);
            items.CopyTo(itemsSpan[start..]);
            return;
        }

        var avail = Capacity - start;
        if (avail >= items.Length)
        {
            // open space all fits without wrap
            items.CopyTo(itemsSpan[start..]);
            return;
        }

        // open space straddles, so copy both segments
        items[..avail].CopyTo(itemsSpan[start..]);
        Assert(items.Length - avail <= _head);
        items[avail..].CopyTo(itemsSpan);
    }

    public void AddRange(params T[] items)
    {
        AddRange(items.AsSpan());
    }

    // this is not the same as a for-loop on items calling AddFront to each! these maintain their forward order.
    public void AddRangeFront(ReadOnlySpan<T> items)
    {
        var want = _used + items.Length;
        if (want > Capacity)
        {
            GrowToAtLeast(want, items.Length);
            items.CopyTo(_items.AsSpan());
            _used += items.Length;
            _head = 0;
            return;
        }

        var itemsSpan = _items.AsSpan();
        var oldHead = _head;
        _head -= items.Length;
        _used += items.Length;

        if (_head >= 0)
        {
            // open space starts after wrap
            items.CopyTo(itemsSpan[_head..]);
            return;
        }

        _head += Capacity;
        var avail = Capacity - _head;
        if (avail >= items.Length)
        {
            // open space all fits without wrap
            items.CopyTo(itemsSpan[_head..]);
            return;
        }

        // open space straddles, so copy both segments
        items[..avail].CopyTo(itemsSpan[_head..]);
        Assert(items.Length - avail <= oldHead);
        items[avail..].CopyTo(itemsSpan);
    }

    public void AddRangeFront(params T[] items)
    {
        AddRangeFront(items.AsSpan());
    }

    public void RemoveAtAndSwapBack(int index)
    {
        if ((uint)index >= (uint)_used)
            throw new ArgumentOutOfRangeException(nameof(index), $"Out of range 0 <= {index} < {_used} (count)");

        var last = AdjustUnchecked(--_used);
        RefAtUnchecked(index) = _items[last];

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            _items[last] = default!;
    }

    public void DropBack()
    {
        if (_used == 0)
            throw new InvalidOperationException("Collection cannot be empty");

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            RefAtUnchecked(_used - 1) = default!;

        --_used;
    }

    public T PopBack()
    {
        if (_used == 0)
            throw new InvalidOperationException("Collection cannot be empty");

        var index = AdjustUnchecked(--_used);
        var item = _items[index];
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            _items[index] = default!;
        return item;
    }

    public void RemoveAtAndSwapFront(int index)
    {
        if ((uint)index >= (uint)_used)
            throw new ArgumentOutOfRangeException(nameof(index), $"Out of range 0 <= {index} < {_used} (count)");

        RefAtUnchecked(index) = _items[_head];
        DropFrontUnchecked();
    }

    public void DropFront()
    {
        if (_used == 0)
            throw new InvalidOperationException("Collection cannot be empty");

        DropFrontUnchecked();
    }

    void DropFrontUnchecked()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            _items[_head] = default!;

        if (++_head == Capacity)
            _head = 0;
        --_used;
    }

    public T PopFront()
    {
        if (_used == 0)
            throw new InvalidOperationException("Collection cannot be empty");

        var item = _items[_head];
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            _items[_head] = default!;

        if (++_head == Capacity)
            _head = 0;
        --_used;

        return item;
    }

    /// <summary>
    /// Rotate the array forward or backward by `count`. After a rotate, an element that was found at list[x] will be
    /// found at list[x+count].
    /// </summary>
    /// <remarks>For best performance, `Count` should be close to `Capacity` so that only the head and a few items need to move.</remarks>
    public void Rotate(int count)
    {
        // unwrap
        if (count >= _used || count <= -_used)
            count %= _used;

        // nothing to do?
        if (count == 0)
            return;

        // rotating negative is same as forward offset by count
        if (count < 0)
            count += _used;

        var capacity = Capacity;
        var oldHead = _head;

        _head -= count;
        if (_head < 0)
            _head += capacity;

        // full array means we don't need to move any elements
        if (_used == capacity)
            return;

        // count backward to avoid stomping if there is range overlap
        for (var i = count - 1; i >= 0; --i)
        {
            var dst = _head + i;
            if (dst >= capacity)
                dst -= capacity;

            var src = oldHead + i + (_used - count);
            if (src >= capacity)
                src -= capacity;

            _items[dst] = _items[src];
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void GrowToAtLeast(int minCapacity, int newHead)
    {
        Assert(minCapacity > Capacity);

        Realloc(Math.Max(minCapacity, Capacity + Capacity / 2), newHead);
    }

    void Realloc(int capacity, int newHead)
    {
        // capacity change always results in realloc and a repack. callers rely on this behavior, don't change it.

        var oldSpans = AsSpans;

        _items = new T[capacity];
        _head = newHead;

        var newSpans = _items.AsSpan()[newHead..];
        oldSpans.Span0.CopyTo(newSpans);
        oldSpans.Span1.CopyTo(newSpans[oldSpans.Span0.Length..]);
    }
}
