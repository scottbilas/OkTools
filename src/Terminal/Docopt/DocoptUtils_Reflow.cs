using System.Text;
using System.Text.RegularExpressions;

namespace OkTools.Terminal;

public class DocoptReflowOptions
{
    public const int DefaultWrapWidth = 120;

    public int DesiredWrapWidth;
    public int MinWrapWidth;
    public int IndentFallback = 15;
}

public static partial class DocoptUtils
{
    // TODO: Reflow loop rewrite!
    // - Get rid of the Select, move to phases.
    // - Operate on current and next section, don't "correct the prev".
    // - Replace "same starting word as next line" extraction with a third regex (though can't apply it the same as
    //   the other two due to needing to extract group1 and compare)
    // - Simplify in general
    // - The "extra indent" should become FirstLineIndent and WrapIndent (how to indent wrapped lines in the same block)

    public static string Reflow(string text, int wrapWidth = 0) =>
        Reflow(text, new DocoptReflowOptions { DesiredWrapWidth = wrapWidth });
    public static string Reflow(string text, DocoptReflowOptions options) =>
        Reflow(text.SelectLinesAsSegments(), options).StringJoin('\n');

    public static IEnumerable<StringSegment> Reflow(IEnumerable<StringSegment> lines, int wrapWidth = 0) =>
        Reflow(lines, new DocoptReflowOptions { DesiredWrapWidth = wrapWidth });

    public static IEnumerable<StringSegment> Reflow(IEnumerable<StringSegment> lines, DocoptReflowOptions options)
    {
        // TODO: support a line break marker, such as a backslash at the end of a line. This would tell reflow not to join
        //       that line with the next.

        // TODO: try to keep [default: foo] together on the same line

        var wrapWidth = options.DesiredWrapWidth;
        if (wrapWidth == 0)
            wrapWidth = DocoptReflowOptions.DefaultWrapWidth;
        else if (wrapWidth <= 0)
            throw new ArgumentOutOfRangeException($"Out of range 0 < {wrapWidth}");

        if (options.MinWrapWidth < 0 || options.MinWrapWidth >= wrapWidth)
            throw new ArgumentOutOfRangeException($"{nameof(options.MinWrapWidth)} out of range 0 <= {options.MinWrapWidth} < {wrapWidth}");

        // TODO: avoid building into stringbuilder if possible and try to just return the segment if unmodified (will be true most of the time for typical case)
        var line = new StringBuilder();
        StringSegment Eol(int newIndent = 0)
        {
            var text = line.ToString();

            line.Clear();

            if (newIndent != 0)
                line.Append(' ', newIndent);

            return new(text);
        }

        var needEol = false;
        foreach (var section in SelectSections(lines).ToArray()) // ToArray is to force _extraIndent to work (needs to modify the prev)
        {
            if (needEol)
            {
                needEol = false;
                yield return Eol();
            }

            if (!section.Text.Any)
            {
                yield return Eol();
                continue;
            }

            // do the indent here so its whitespace doesn't get caught up in the calculations below
            // (TODO: this breaks very narrow wrapping..)
            line.Append(section.Text[..section.TotalIndent].Span);
            var sectionText = section.Text[section.TotalIndent..];

            // special: if we have a really wide column indent, let's fall back to non aligned
            // (TODO: also broken with very narrow wrapping..)
            var indent = section.TotalIndent;
            if (options.IndentFallback != 0 && wrapWidth - indent < options.IndentFallback)
            {
                indent = section.Text.GetTrimStart() + 1;
                yield return Eol(indent);
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
                line.Append(sectionText[..write].TrimEnd().Span);
                sectionText = sectionText[write..].TrimStart();
                if (!sectionText.Any)
                    break;

                yield return Eol(indent);
            }

            needEol = true;
        }

        yield return Eol();
    }

    static IEnumerable<Section> SelectSections(IEnumerable<StringSegment> lines)
    {
        Section? lastSection = null;

        foreach (var line in lines)
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

    partial record Section(StringSegment Text, int Indent)
    {
        int _extraIndent;

        public Section(StringSegment text) : this(text.TrimEnd(), 0)
        {
            var indentMatch = Text.Match(IndentRx0());
            if (indentMatch.Success)
                Indent = indentMatch.Index - Text.SegmentStart + indentMatch.Length;
            else
            {
                indentMatch = Text.Match(IndentRx1());
                if (indentMatch.Success)
                    Indent = indentMatch.Index - Text.SegmentStart + indentMatch.Length;
                else
                    Indent = Text.GetTrimStart();
            }
        }

        // these regexes find where we should indent to upon wrapping, in priority order.
        //
        // - align to the right side of a "docopt divider" (>= 2 spaces). higher pri to catch bulleted option lists.
        [GeneratedRegex(@"\S {2,}")]
        private static partial Regex IndentRx0();
        // - align to the text part of a bullet point, number, or comment: * - // # 1.
        [GeneratedRegex(@"^ *([-*#]|//|\d+\.) ")]
        private static partial Regex IndentRx1();

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
                    var text1 = other.Text.SliceSafe(Indent, wordLen);
                    if (text0.Equals(text1))
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
}
