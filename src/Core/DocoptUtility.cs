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

            if (!section.Text.Any)
            {
                result.Append(options.Eol);
                continue;
            }

            // do the indent here so its whitespace doesn't get caught up in the calculations below
            // (TODO: this breaks very narrow wrapping..)
            result.Append(section.Text[..section.TotalIndent].Span);
            var sectionText = section.Text[section.TotalIndent..];

            // special: if we have a really wide column indent, let's fall back to non aligned
            // (TODO: also broken with very narrow wrapping..)
            var indent = section.TotalIndent;
            if (options.IndentFallback != 0 && wrapWidth - indent < options.IndentFallback)
            {
                indent = section.Text.GetTrimStart() + 1;
                result.Append(options.Eol);
                result.Append(' ', indent);
            }

            for (;;)
            {
                // this is how much space we have to write into
                var available = Math.Max(Math.Min(wrapWidth - indent, sectionText.Length), 1);

                // try to find a reasonable place to break, otherwise do a hard break at the width limit
                var write = sectionText.Length;
                if (write > available)
                {
                    write = sectionText.String.LastIndexOf(' ', sectionText.SegmentStart + available, available+1);
                    if (write >= 0)
                        write -= sectionText.SegmentStart;
                    if (write < options.MinWrapWidth)
                        write = available;
                }

                // write what will fit and advance
                result.Append(sectionText[..write].TrimEnd());
                sectionText = sectionText[write..].TrimStart();
                if (!sectionText.Any)
                    break;

                result.Append(options.Eol);
                result.Append(' ', indent);
            }

            needEol = true;
        }

        return result.ToString();
    }

    record Section(StringSegment Text, int Indent)
    {
        int _extraIndent;

        public Section(StringSegment text) : this(text.TrimEnd(), 0)
        {
            var indentMatch = Text.Match(s_indentRx0);
            if (indentMatch.Success)
                Indent = indentMatch.Index - Text.SegmentStart + indentMatch.Length;
            else
            {
                indentMatch = Text.Match(s_indentRx1);
                if (indentMatch.Success)
                    Indent = indentMatch.Index - Text.SegmentStart + indentMatch.Length;
                else
                    Indent = Text.GetTrimStart();
            }
        }

        public int TotalIndent => Indent + _extraIndent;
        bool HasPrefix => Text.GetTrimStart() < Indent; // TODO: "extra indent" and "prefix" concepts do the same thing; join them

        // note that this may modify the previous section if we detect duplicate leading words
        public Section? MergeWith(Section other)
        {
            if (Text.IsEmpty || other.Text.IsEmpty || Indent != other.Indent || other.HasPrefix)
                return null;

            if (!HasPrefix)
            {
                // if the leading word is identical (common with program name in 'usage' lines), do not merge
                var end = Text[Indent..].IndexOf(' ');
                if (end >= 0)
                {
                    var wordLen = end + 1; // include the space
                    var text0 = Text[Indent..(Indent+wordLen)];
                    var text1 = other.Text[Indent..(Indent+wordLen)];
                    if (text0.StringEquals(text1))
                    {
                        _extraIndent = other._extraIndent = wordLen;
                        return null;
                    }
                }
            }

            // $$$ TODO: get rid of this silliness
            var newText = Text.ToString() + ' ' + other.Text.TrimStart();
            return this with { Text = new StringSegment(newText) };
        }

        public override string ToString() => $"{Text.ToDebugString()}; indent={Indent}, prefix={HasPrefix}";
    }

    // these regexes find where we should indent to upon wrapping, in priority order.
    //
    // - align to the right side of a "docopt divider" (>= 2 spaces). higher pri to catch bulleted option lists.
    static readonly Regex s_indentRx0 = new(@"\S {2,}");
    // - align to the text part of a '*' or '-' style bullet point
    static readonly Regex s_indentRx1 = new(@"^ *([-*]|\d+\.) ");

    static IEnumerable<StringSegment> SelectLines(string text)
    {
        for (var start = 0; start != text.Length;)
        {
            var end = text.IndexOf('\n', start);

            var next = end;
            if (end < 0)
                end = next = text.Length;
            else
                ++next;

            yield return new StringSegment(text, start, end - start);
            start = next;
        }
    }

    static IEnumerable<Section> SelectSections(string text)
    {
        Section? lastSection = null;

        foreach (var line in SelectLines(text))
        {
            var newSection = new Section(line);

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
