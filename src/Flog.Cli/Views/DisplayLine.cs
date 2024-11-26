using System.Diagnostics;

[DebuggerDisplay("\"{Span.Slice(0, Length <= 30 ? Length : 30)}\" (len={Length}, remain={_chars.Length - Length})")]
readonly struct DisplayLine
{
    DisplayLine(int lineIndex, string chars, int begin, int end)
    {
        Debug.Assert(lineIndex >= 0);
        Debug.Assert(begin >= 0 && begin <= end);
        Debug.Assert(end >= 0 && end <= chars.Length);

        LineIndex = lineIndex;
        _chars = chars;
        _begin = begin;
        _end = end;
    }

    public readonly int LineIndex;

    readonly string _chars;
    readonly int _begin, _end;

    public bool IsValid => _chars != null;
    public int Length => _end - _begin;
    public int Remain => _chars.Length - _end;
    public ReadOnlySpan<char> Span => _chars.AsSpan(_begin, Length);

    public bool NeedsLeadingTruncateMarker => _begin != 0;
    public bool NeedsTrailingTruncateMarker => _end != _chars.Length;

    public DisplayLine RemainTruncated(int maxWidth, int truncMarkerWidth)
    {
        maxWidth -= truncMarkerWidth; // continuation lines have a leading trunc marker

        var width = Remain;
        if (width > maxWidth)
            width = maxWidth - truncMarkerWidth;

        return new(LineIndex, _chars, _end, _end + width);
    }

    public static DisplayLine NewTruncated(int lineIndex, string line, int maxWidth, int truncMarkerWidth)
    {
        var width = line.Length;
        if (width > maxWidth)
            width = maxWidth - truncMarkerWidth;
        return new(lineIndex, line, 0, width);
    }

    public static DisplayLine New(int lineIndex, string line) =>
        new(lineIndex, line, 0, line.Length);
    public static DisplayLine NewOrDefault(int lineIndex, string? line) =>
        line != null ? New(lineIndex, line) : default;
}

