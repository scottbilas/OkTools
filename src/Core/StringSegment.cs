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

[PublicAPI]
[DebuggerDisplay("{ToDebugString()}")]
public readonly struct StringSegment : IEquatable<StringSegment>
{
    // TODO: TESTS

    readonly string _string;
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
        var str = _string
            .Substring(_offset, _length)
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return $"'{str}' (start={_offset}, end={SegmentEnd}, len={_length})";
    }

    public override string ToString() => _string[SegmentStart..SegmentEnd];

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
    public string String       => _string;
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
            return _string[_offset + index];
        }
    }

    public StringSegment Slice(int start, int length) => new(this, start, length);

    public int IndexOf(char value) =>
        Span.IndexOf(value);
    public int IndexOf(char value, int startIndex) =>
        AsSpan(startIndex).IndexOf(value) + startIndex;
    public int IndexOf(char value, int startIndex, int count) =>
        AsSpan(startIndex, count).IndexOf(value) + startIndex;

    public int GetTrimStart()
    {
        var i = 0;
        for (; i != _length; ++i)
        {
            if (!char.IsWhiteSpace(_string[_offset + i]))
                break;
        }
        return i;
    }

    public int GetTrimEnd()
    {
        var i = _length;
        for (; i > 0; --i)
        {
            if (!char.IsWhiteSpace(_string[_offset + i-1]))
                break;
        }

        return i;
    }

    public StringSegment TrimStart() => this[GetTrimStart()..];
    public StringSegment TrimEnd()   => this[..GetTrimEnd()];
    public StringSegment Trim()      => this[GetTrimStart()..GetTrimEnd()];

    public Match Match(Regex regex) => regex.Match(_string, _offset, _length);

    public bool StringEquals(StringSegment other, StringComparison comparison = StringComparison.Ordinal) =>
        _length == other._length && StringCompare(other) == 0;

    public int StringCompare(StringSegment other, StringComparison comparison = StringComparison.Ordinal) =>
        string.Compare(_string, _offset, other._string, other._offset, _length, comparison);

    public bool Equals(StringSegment other) =>
        ReferenceEquals(_string, other._string) && _offset == other._offset && _length == other._length;
    public bool Equals(string other, StringComparison comparison = StringComparison.Ordinal) =>
        string.Compare(_string, _offset, other, 0, Math.Max(_length, other.Length), comparison) == 0;

    public override bool Equals(object? obj) =>
        obj switch
        {
            StringSegment other when Equals(other) => true,
            string other when Equals(other) => true,
            _ => false,
        };

    public override int GetHashCode() => HashCode.Combine(_string, _offset, _length);

    public static bool operator ==(StringSegment left, StringSegment right) => left.Equals(right);
    public static bool operator !=(StringSegment left, StringSegment right) => !left.Equals(right);

    public static bool operator ==(StringSegment left, string right) => left.Equals(right);
    public static bool operator !=(StringSegment left, string right) => !left.Equals(right);

    public static bool operator ==(string left, StringSegment right) => right.Equals(left);
    public static bool operator !=(string left, StringSegment right) => !right.Equals(left);
}
