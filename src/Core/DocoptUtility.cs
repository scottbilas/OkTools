using System.Text;
using System.Text.RegularExpressions;

namespace OkTools.Core;

public class DocoptReflowOptions
{
    public int MinWrapWidth;
    public int IndentFallback = 15;
    public string Eol = "\n";
}

[PublicAPI]
public static class DocoptUtility
{
    // TODO: nothing about this should be specific to docopt.
    //       can use blank lines to mean sections, detecting alignment, and - most importantly - the
    //       "end of last double space" rule to mean the indent point for wrapping.

    public static string Reflow(string text, int wrapWidth) => Reflow(text, wrapWidth, new DocoptReflowOptions());

    public static string Reflow(string text, int wrapWidth, DocoptReflowOptions options)
    {
        // TODO: support a line break marker, such as a backslash at the end of a line. This would tell reflow not to join
        //       that line with the next.

        // TODO: try to keep [default: foo] together on the same line

        // TODO: make the max wrap width optional. right now i'm just arbitrarily picking something big-ish. too big and
        // it's just ridiculous to try to read..

        if (wrapWidth > 120)
            wrapWidth = 120;

        if (wrapWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(wrapWidth), $"Out of range 0 < {wrapWidth}");
        if (options.MinWrapWidth < 0 || options.MinWrapWidth >= wrapWidth)
            throw new ArgumentOutOfRangeException(nameof(wrapWidth), $"{nameof(options.MinWrapWidth)} out of range 0 <= {options.MinWrapWidth} < {wrapWidth}");

        var result = new StringBuilder();

        var needEol = false;
        foreach (var section in SelectSections(text).ToArray()) // ToArray is to force _extraIndent to work (needs to modify the prev)
        {
            if (needEol)
            {
                result.Append(options.Eol);
                needEol = false;
            }

            if (!section.Span.Any)
            {
                result.Append(options.Eol);
                continue;
            }

            // do the indent here so its whitespace doesn't get caught up in the calculations below
            // (TODO: this breaks very narrow wrapping..)
            result.Append(section.Span.WithLength(section.TotalIndent).Span);
            var span = section.Span.WithOffsetStart(section.TotalIndent);

            // special: if we have a really wide column indent, let's fall back to non aligned
            // (TODO: also broken with very narrow wrapping..)
            var indent = section.TotalIndent;
            if (options.IndentFallback != 0 && wrapWidth - indent < options.IndentFallback)
            {
                indent = section.Span.TrimStartOffset()+1;
                result.Append(options.Eol);
                result.Append(' ', indent);
            }

            for (;;)
            {
                // this is how much space we have to write into
                var available = Math.Max(Math.Min(wrapWidth - indent, span.Length), 1);

                // try to find a reasonable place to break, otherwise do a hard break at the width limit
                var write = span.Length;
                if (write > available)
                {
                    write = span.Text.LastIndexOf(' ', span.Start + available, available+1);
                    if (write >= 0)
                        write -= span.Start;
                    if (write < options.MinWrapWidth)
                        write = available;
                }

                // write what will fit and advance
                result.Append(span.Text, span.Start, span.TrimEndIndex(span.Start + write) - span.Start);
                span = span.WithOffsetStart(write).TrimStart();
                if (!span.Any)
                    break;

                result.Append(options.Eol);
                result.Append(' ', indent);
            }

            needEol = true;
        }

        return result.ToString();
    }

    record Section(StringSpan Span, int Indent)
    {
        int _extraIndent;

        public Section(StringSpan span) : this(span.TrimEnd(), 0)
        {
            var indentMatch = Span.Match(s_indentRx0);
            if (indentMatch.Success)
                Indent = indentMatch.Index - Span.Start + indentMatch.Length;
            else
            {
                indentMatch = Span.Match(s_indentRx1);
                if (indentMatch.Success)
                    Indent = indentMatch.Index - Span.Start + indentMatch.Length;
                else
                    Indent = Span.TrimStartOffset();
            }
        }

        public int TotalIndent => Indent + _extraIndent;
        bool HasPrefix => Span.TrimStartOffset() < Indent; // TODO: "extra indent" and "prefix" concepts do the same thing; join them

        // note that this may modify the previous section if we detect duplicate leading words
        public Section? MergeWith(Section other)
        {
            if (Span.IsEmpty || other.Span.IsEmpty || Indent != other.Indent || other.HasPrefix)
                return null;

            if (!HasPrefix)
            {
                // if the leading word is identical (common with program name in 'usage' lines), do not merge
                var end = Span[Indent..].IndexOf(' ');
                if (end >= 0)
                {
                    var wordLen = end + 1; // include the space
                    var span0 = Span[Indent..].WithLength(wordLen);
                    var span1 = other.Span[Indent..].WithLength(wordLen);
                    if (span0.TextEquals(span1))
                    {
                        _extraIndent = other._extraIndent = wordLen;
                        return null;
                    }
                }
            }

            // $$$ TODO: get rid of this silliness
            var newText = Span.ToString() + ' ' + other.Span.TrimStart();
            return this with { Span = new StringSpan(newText) };
        }

        public override string ToString() => $"{Span.ToDebugString()}; indent={Indent}, prefix={HasPrefix}";
    }

    // these regexes find where we should indent to upon wrapping, in priority order.
    //
    // - align to the right side of a "docopt divider" (>= 2 spaces). higher pri to catch bulleted option lists.
    static readonly Regex s_indentRx0 = new(@"\S {2,}");
    // - align to the text part of a '*' or '-' style bullet point
    static readonly Regex s_indentRx1 = new(@"^ *([-*]|\d+\.) ");

    static IEnumerable<StringSpan> SelectLines(string text)
    {
        for (var start = 0; start != text.Length;)
        {
            var end = text.IndexOf('\n', start);

            var next = end;
            if (end < 0)
                end = next = text.Length;
            else
                ++next;

            yield return new StringSpan(text, start, end);
            start = next;
        }
    }

    static IEnumerable<Section> SelectSections(string text)
    {
        Section? lastSection = null;

        foreach (var span in SelectLines(text))
        {
            var newSection = new Section(span);

            if (lastSection != null)
            {
                var merged = lastSection.MergeWith(newSection);
                if (merged != null)
                    lastSection = merged;
                else
                {
                    yield return lastSection;
                    lastSection = newSection;
                }
            }
            else
                lastSection = newSection;
        }

        if (lastSection != null)
            yield return lastSection;
    }
}
