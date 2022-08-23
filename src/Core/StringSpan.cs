using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OkTools.Core;

[PublicAPI]
[DebuggerDisplay("{ToDebugString()}")]
public readonly struct StringSpan : IEquatable<StringSpan>
{
    // TODO: TESTS

    public readonly string Text;
    public readonly int Start, End;

    public StringSpan(string text)
    {
        Text = text;
        Start = 0;
        End = text.Length;
    }

    public StringSpan(string text, int start, int end)
    {
        if (start < 0 || start > text.Length)
            throw new ArgumentOutOfRangeException(nameof(start), $"Out of range 0 <= {start} <= {text.Length}");
        if (end < start || end > text.Length)
            throw new ArgumentOutOfRangeException(nameof(end), $"Out of range {start} <= {end} <= {text.Length}");

        Text = text;
        Start = start;
        End = end;
    }

    public ReadOnlySpan<char> Span => Text.AsSpan(Start, Length);

    public int Length => End - Start;
    public bool IsEmpty => Start == End;
    public bool Any => Start != End;

    public char this[int index]
    {
        get
        {
            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException(nameof(index), $"Out of range 0 <= {index} < {Length}");
            return Text[Start + index];
        }
    }

    public StringSpan WithStart(int start) => new(Text, start, End);
    public StringSpan WithOffsetStart(int offset) => new(Text, Start + offset, End);
    public StringSpan WithEnd(int end) => new(Text, Start, end);
    public StringSpan WithOffsetEnd(int offset) => new(Text, Start, End + offset);
    public StringSpan WithLength(int len) => new(Text, Start, Start + len);

    public static StringSpan Empty => new("", 0, 0);

    public int TrimStartIndex(int fromStart)
    {
        var i = fromStart;
        for (; i != End; ++i)
        {
            if (!char.IsWhiteSpace(Text[i]))
                break;
        }

        return i;
    }
    public int TrimStartIndex() => TrimStartIndex(Start);
    public int TrimStartOffset() => TrimStartIndex() - Start;
    public StringSpan TrimStart() => new(Text, TrimStartIndex(), End);

    public int TrimEndIndex(int fromEnd)
    {
        var i = fromEnd;
        for (; i > Start; --i)
        {
            if (!char.IsWhiteSpace(Text[i-1]))
                break;
        }

        return i;
    }

    public int TrimEndIndex() => TrimEndIndex(End);
    public int TrimEndOffset() => TrimEndIndex() - Start;
    public StringSpan TrimEnd() => new(Text, Start, TrimEndIndex());

    public Match Match(Regex regex) => regex.Match(Text, Start, Length);

    public string ToDebugString()
    {
        var text = Text
            .Substring(Start, Length)
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return $"'{text}' (start={Start}, end={End}, len={Length})";
    }

    public override string ToString() => Text.Substring(Start, Length);

    public bool Equals(StringSpan other)
    {
        return Text == other.Text && Start == other.Start && End == other.End;
    }

    public bool Equals(string other)
    {
        return string.Compare(Text, Start, other, 0, Length, StringComparison.Ordinal) == 0;
    }

    public override bool Equals(object? obj) =>
        obj switch
        {
            StringSpan other when Equals(other) => true,
            string other when Equals(other) => true,
            _ => false,
        };

    public override int GetHashCode()
    {
        return HashCode.Combine(Text, Start, End);
    }

    public static bool operator ==(StringSpan left, StringSpan right) => left.Equals(right);
    public static bool operator !=(StringSpan left, StringSpan right) => !left.Equals(right);

    public static bool operator ==(StringSpan left, string right) => left.Equals(right);
    public static bool operator !=(StringSpan left, string right) => !left.Equals(right);

    public static bool operator ==(string left, StringSpan right) => right.Equals(left);
    public static bool operator !=(string left, StringSpan right) => !right.Equals(left);
}
