using System.Diagnostics;

[DebuggerDisplay("\"{Span.Slice(0, Length <= 30 ? Length : 30)}\" (len={Length}, remain={m_chars.Length - Length})")]
readonly struct DisplayLine
{
    DisplayLine(int lineIndex, string chars, int begin, int end)
    {
        Debug.Assert(lineIndex >= 0);
        Debug.Assert(begin >= 0 && begin <= end);
        Debug.Assert(end >= 0 && end <= chars.Length);

        LineIndex = lineIndex;
        m_chars = chars;
        m_begin = begin;
        m_end = end;
    }

    public readonly int LineIndex;

    readonly string m_chars;
    readonly int m_begin, m_end;

    public bool IsValid => m_chars != null;
    public int Length => m_end - m_begin;
    public int Remain => m_chars.Length - m_end;
    public ReadOnlySpan<char> Span => m_chars.AsSpan(m_begin, Length);

    public bool NeedsLeadingTruncateMarker => m_begin != 0;
    public bool NeedsTrailingTruncateMarker => m_end != m_chars.Length;

    public DisplayLine RemainTruncated(int maxWidth, int truncMarkerWidth)
    {
        maxWidth -= truncMarkerWidth; // continuation lines have a leading trunc marker

        var width = Remain;
        if (width > maxWidth)
            width = maxWidth - truncMarkerWidth;

        return new(LineIndex, m_chars, m_end, m_end + width);
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

