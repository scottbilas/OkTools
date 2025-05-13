using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace OkTools.Core;

// TODO: throw in some AggressiveInlining like they do in .net span

public static class StringSegmentOperations
{
    public static void Append(this StringBuilder @this, StringSegment segment) =>
        @this.Append(segment.String, segment.SegmentStart, segment.Length);
}

// TODO: consider getting rid of this in favor of ReadOnlyMemory<char> extensions
[PublicAPI]
[DebuggerDisplay("{ToDebugString()}")]
public readonly struct StringSegment :
    IEquatable<StringSegment>,
#   if NET9_0_OR_GREATER
    IEquatable<ReadOnlySpan<char>>,
#   endif
    IEquatable<ReadOnlyMemory<char>>,
    IEquatable<string>,
    IEnumerable<char>
{
    // TODO: TESTS

    readonly string? _string; // nullable because we're a struct
    readonly int _offset, _length;

    public StringSegment(string str)
    {
        _string = str;
        _offset = 0;
        _length = str.Length;
    }

    public StringSegment(string str, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > str.Length)
            throw new ArgumentOutOfRangeException($"Out of range 0 <= {offset} <= {offset + length} <= {str.Length}");

        _string = str;
        _offset = offset;
        _length = length;
    }

    public StringSegment(in StringSegment src, int start, int length)
    {
        if (start < 0 || length < 0 || start + length > src._length)
            throw new ArgumentOutOfRangeException($"Out of range 0 <= {start} <= {start + length} <= {src._length}");

        _string = src._string;
        _offset = src._offset + start;
        _length = length;
    }

    public string ToDebugString()
    {
        var str = String
            .Substring(_offset, _length)
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return $"'{str}' (start={_offset}, end={SegmentEnd}, len={_length})";
    }

    public override string ToString() => _length > 0 ? _string!.Substring(_offset, _length) : ""; // no alloc if full string

    public ReadOnlySpan<char> Span => _string.AsSpan(_offset, _length);

    public ReadOnlySpan<char> AsSpan(int start)
    {
        if (start < 0 || start > _length)
            throw new ArgumentOutOfRangeException($"Out of range 0 <= {start} <= {_length}");

        return _string.AsSpan(_offset + start, _length - start);
    }

    public ReadOnlySpan<char> AsSpan(int start, int length)
    {
        if (start < 0 || length < 0 || start + length > _length)
            throw new ArgumentOutOfRangeException($"Out of range 0 <= {start} <= {start + length} <= {_length}");

        return _string.AsSpan(_offset + start, length);
    }

    public int    SegmentStart => _offset;
    public int    SegmentEnd   => _offset + _length;
    public string String       => _string ?? "";
    public int    Length       => _length;
    public bool   IsEmpty      => _length == 0;
    public bool   Any          => _length != 0;

    public static readonly StringSegment Empty = new("", 0, 0);

    public char this[int index]
    {
        get
        {
            if (index < 0 || index >= _length)
                throw new ArgumentOutOfRangeException(nameof(index), $"Out of range 0 <= {index} < {_length}");
            return _string![_offset + index];
        }
    }

    public StringSegment Slice(int start, int length) => new(this, start, length);

    public bool StartsWith(char value) =>
        _length != 0 && _string![_offset] == value;
    public bool StartsWith(ReadOnlySpan<char> value) =>
        Span.StartsWith(value);
    public bool StartsWith(ReadOnlySpan<char> value, StringComparison comparison) =>
        Span.StartsWith(value, comparison);
    public bool EndsWith(char value) =>
        _length != 0 && _string![_offset + _length - 1] == value;
    public bool EndsWith(ReadOnlySpan<char> value) =>
        Span.EndsWith(value);
    public bool EndsWith(ReadOnlySpan<char> value, StringComparison comparison) =>
        Span.EndsWith(value, comparison);

    public int IndexOf(char value) =>
        Span.IndexOf(value);
    public int IndexOf(char value, int startIndex) =>
        AsSpan(startIndex).IndexOf(value) + startIndex;
    public int IndexOf(char value, int startIndex, int count) =>
        AsSpan(startIndex, count).IndexOf(value) + startIndex;

    public int IndexOf(string value) =>
        Span.IndexOf(value);
    public int IndexOf(string value, int startIndex) =>
        AsSpan(startIndex).IndexOf(value) + startIndex;
    public int IndexOf(string value, int startIndex, int count) =>
        AsSpan(startIndex, count).IndexOf(value) + startIndex;

    public int GetTrimStart()
    {
        var i = 0;
        for (; i != _length; ++i)
        {
            if (!char.IsWhiteSpace(_string![_offset + i]))
                break;
        }
        return i;
    }

    public int GetTrimEnd()
    {
        var i = _length;
        for (; i > 0; --i)
        {
            if (!char.IsWhiteSpace(_string![_offset + i-1]))
                break;
        }

        return i;
    }

    public static implicit operator StringSegment(string value) => new(value);
    public static implicit operator ReadOnlySpan<char>(StringSegment value) => value.Span;

    public StringSegment TrimStart() => this[GetTrimStart()..];
    public StringSegment TrimEnd()   => this[..GetTrimEnd()];
    public StringSegment Trim()      => this[GetTrimStart()..GetTrimEnd()];

    public Match Match(Regex regex) => regex.Match(String, _offset, _length);

    public int Compare(StringSegment other, StringComparison comparison = StringComparison.Ordinal) =>
        Span.CompareTo(other.Span, comparison);
    public int Compare(ReadOnlyMemory<char> other, StringComparison comparison = StringComparison.Ordinal) =>
        Span.CompareTo(other.Span, comparison);
    public int Compare(ReadOnlySpan<char> other, StringComparison comparison = StringComparison.Ordinal) =>
        Span.CompareTo(other, comparison);
    public int Compare(string other, StringComparison comparison = StringComparison.Ordinal) =>
        Span.CompareTo(other.AsSpan(), comparison);

    public bool Equals(StringSegment other, StringComparison comparison) =>
        Span.Equals(other.Span, comparison);
    public bool Equals(ReadOnlyMemory<char> other, StringComparison comparison) =>
        Span.Equals(other.Span, comparison);
    public bool Equals(ReadOnlySpan<char> other, StringComparison comparison) =>
        Span.Equals(other, comparison);
    public bool Equals(string other, StringComparison comparison) =>
        Span.Equals(other.AsSpan(), comparison);

    public bool Equals(StringSegment other) =>
        Span.Equals(other.Span, StringComparison.Ordinal);
    public bool Equals(ReadOnlyMemory<char> other) =>
        Span.Equals(other.Span, StringComparison.Ordinal);
    public bool Equals(ReadOnlySpan<char> other) =>
        Span.Equals(other, StringComparison.Ordinal);
    public bool Equals(string? other) =>
        other != null && Span.Equals(other.AsSpan(), StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj switch
        {
            StringSegment other => Equals(other),
            ReadOnlyMemory<char> other => Equals(other),
            string other => Equals(other),
            _ => false,
        };

    public override int GetHashCode() => HashCode.Combine(String, _offset, _length);

    public IEnumerator<char> GetEnumerator()
    {
        for (var i = 0; i < _length; ++i)
            yield return _string![_offset + i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(StringSegment left, StringSegment right) => left.Equals(right);
    public static bool operator !=(StringSegment left, StringSegment right) => !left.Equals(right);

    public static bool operator ==(StringSegment left, string right) => left.Equals(right);
    public static bool operator !=(StringSegment left, string right) => !left.Equals(right);

    public static bool operator ==(string left, StringSegment right) => right.Equals(left);
    public static bool operator !=(string left, StringSegment right) => !right.Equals(left);
}
