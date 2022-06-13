using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// this is a simpler ReadOnlyMemory more tuned to my needs

sealed class OkSegmentDebugView<T>
{
    readonly OkSegment<T> _segment;

    /* TODO if ever implement OkSegmentRO
    public OkSegmentDebugView(OkSegmentRO<T> segment) => _segment = segment
    */
    public OkSegmentDebugView(OkSegment<T> segment) => _segment = segment;

    // ReSharper disable once ReturnTypeCanBeEnumerable.Global
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items => _segment.Span.ToArray();
}

[DebuggerTypeProxy(typeof(OkSegmentDebugView<>))]
[DebuggerDisplay("{ToString(),raw}")]
readonly struct OkSegment<T> : IEquatable<OkSegment<T>>
{
    readonly object? _object; // can be T[] or T (or null)
    readonly int _start, _length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OkSegment(T[]? array) : this(array, 0, array?.Length ?? 0) {}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OkSegment(T[]? array, int start, int length) =>
        this = new OkSegment<T>(array).Slice(start, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OkSegment(T item) : this(item, 0, 1) {}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    OkSegment(object? obj, int start, int length)
    {
        Debug.Assert(obj is null or T or T[]);

        _object = obj;
        _start  = start;
        _length = length;
    }

    public static implicit operator OkSegment<T>(T[]? array) => new(array);
    public static implicit operator OkSegment<T>(T obj) => new(obj);

    // ReSharper disable once ConvertToAutoPropertyWhenPossible
    public int Length => _length;

    public override string ToString() => $"OkSegmentRO<{typeof(T).Name}>[{_length}]";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OkSegment<T> Slice(int start)
    {
        if ((uint)start > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(start));

        return new OkSegment<T>(_object, _start + start, _length - start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OkSegment<T> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
            throw new ArgumentOutOfRangeException(nameof(start));

        return new OkSegment<T>(_object, _start + start, length);
    }

    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            switch (_object)
            {
                case null:
                    return default;
                case T[] array:
                    return array.AsSpan();
                default:
                {
                    var obj = (T)_object;
                    return MemoryMarshal.CreateSpan(ref obj, 1);
                }
            }
        }
    }

    public Span<T>.Enumerator GetEnumerator() => Span.GetEnumerator();

    public bool Equals(OkSegment<T> other) =>
        _object == other._object &&
        _start  == other._start  &&
        _length == other._length;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals([NotNullWhen(true)] object? obj) => throw new InvalidOperationException("Don't box me");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode() => throw new InvalidOperationException("Don't box me");
}
