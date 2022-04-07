using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OkTools.Core;

[PublicAPI]
[DebuggerDisplay("{ToDebugString()}")]
public readonly struct StringSpan
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
            throw new IndexOutOfRangeException(nameof(start) + $" out of range 0 <= {start} <= {text.Length}");
        if (end < start || end > text.Length)
            throw new IndexOutOfRangeException(nameof(end) + $" out of range {start} <= {end} <= {text.Length}");

        Text = text;
        Start = start;
        End = end;
    }

    public int Length => End - Start;
    public bool IsEmpty => Start == End;
    public bool Any => Start != End;

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
}
