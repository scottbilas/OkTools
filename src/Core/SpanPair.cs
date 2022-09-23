using System.Runtime.CompilerServices;

// ReSharper disable ReplaceSliceWithRangeIndexer

namespace OkTools.Core;

// needed because tuples with Span<> not supported (see https://stackoverflow.com/questions/52484998/the-type-spanchar-may-not-be-used-as-a-type-argument#comment130237088_52485647)
[PublicAPI]
public readonly ref struct SpanPair<T>
{
    public readonly Span<T> Span0, Span1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanPair(Span<T> span0, Span<T> span1 = default)
    {
        Span0 = span0;
        Span1 = span1;
    }

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (index < Span0.Length)
                return ref Span0[index];
            return ref Span1[index - Span0.Length];
        }
    }

    public Enumerator GetEnumerator() => new(this);

    public int Length => Span0.Length + Span1.Length;
    public bool Any => Length != 0;
    public bool IsEmpty => Length == 0;

    public static bool operator ==(SpanPair<T> left, SpanPair<T> right) => left.Span0 == right.Span0 && left.Span1 == right.Span1;
    public static bool operator !=(SpanPair<T> left, SpanPair<T> right) => !(left == right);

    public override bool Equals(object? obj) => throw new InvalidOperationException($"{nameof(Equals)} cannot be used on {nameof(SpanPair<T>)}");
    public override int GetHashCode() => throw new InvalidOperationException($"{nameof(GetHashCode)} cannot be used on {nameof(SpanPair<T>)}");

    public override string ToString() => $"{Span0.ToString()}:{Span1.ToString()}";

    public void Clear()
    {
        Span0.Clear();
        Span1.Clear();
    }

    public void Fill(T value)
    {
        Span0.Fill(value);
        Span1.Fill(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanPair<T> Slice(int start)
    {
        return start >= Span0.Length
            ? new(Span1.Slice(start - Span0.Length)) // entirely in Span1
            : new(Span0.Slice(start), Span1);        // starts in Span0 so must still split
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanPair<T> Slice(int start, int length)
    {
        // entirely in Span1
        if (start >= Span0.Length)
            return new(Span1.Slice(start - Span0.Length, length));

        // entirely in Span0
        if (start + length <= Span0.Length)
            return new(Span0.Slice(start, length));

        // still split
        var span0 = Span0.Slice(start);
        return new(span0, Span1.Slice(0, length - span0.Length));
    }

    public T[] ToArray()
    {
        if (Length == 0)
            return Array.Empty<T>();

        var destination = new T[Length];
        Span0.CopyTo(destination);
        Span1.CopyTo(destination.AsSpan(Span0.Length));

        return destination;
    }

    public ref struct Enumerator
    {
        Span<T> _span, _spanNext;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(SpanPair<T> pair)
        {
            if (pair.Span0.IsEmpty)
                _span = pair.Span1;
            else
            {
                _span = pair.Span0;
                _spanNext = pair.Span1;
            }

            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            var index = _index + 1;
            if (index >= _span.Length)
                return Advance();

            _index = index;
            return true;

        }

        bool Advance()
        {
            if (_spanNext.IsEmpty)
                return false;

            _span = _spanNext;
            _spanNext = default;
            _index = -1;

            return MoveNext();
        }

        public ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _span[_index];
        }
    }
}
