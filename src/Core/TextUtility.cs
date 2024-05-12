using System.Text;
using System.Text.RegularExpressions;

namespace OkTools.Core;

[PublicAPI]
public static class TextUtility
{
    /// <summary>
    /// Scans `text` for EOL sequences (\n, \r\n) and returns the most common seen. Old style mac sequences (plain \r) are not supported and instead treated as unix (plain \n).
    /// </summary>
    /// <param name="text">The text to scan for EOL's.</param>
    /// <param name="stopAfterWindow">Once any of the candidate EOL types is "winning" by this amount, stop and return it. Set to 0 to force a scan of all of `text`.</param>
    /// <returns>Either "\n" (unix), or "\r\n" (dos/windows), whichever is most common. If no newlines found, returns the environment default.</returns>
    public static string DetectEolType(string text, int stopAfterWindow = 50)
    {
        int crlfCount = 0, lfCount = 0;
        for (var i = 0; i < text.Length; ++i)
        {
            if (text[i] == '\r')
            {
                if (i < text.Length - 1 && text[i + 1] == '\n')
                {
                    ++crlfCount;
                    ++i;
                }
                else
                {
                    // old mac style, treat as unix
                    ++lfCount;
                }
            }
            else if (text[i] == '\n')
            {
                ++lfCount;
            }

            if (stopAfterWindow > 0 && Math.Abs(crlfCount - lfCount) >= stopAfterWindow)
                break;
        }

        if (crlfCount > lfCount)
            return "\r\n";
        if (lfCount > crlfCount)
            return "\n";

        // it's either a tie, or we didn't find any. in that case, go with whatever the os wants.
        return Environment.NewLine;
    }

    public static int ToFourCc(string text)
    {
        if (text.Length != 4)
            throw new ArgumentException("Must be exactly 4 characters long", nameof(text));

        return text[0] | (text[1] << 8) | (text[2] << 16) | (text[3] << 24);
    }

    public static string WildcardToRegexText(string wildcard) =>
        "^" + Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";

    public static Regex WildcardToRegex(string wildcard, RegexOptions rxOptions = RegexOptions.IgnoreCase) =>
        new(WildcardToRegexText(wildcard), rxOptions);

    public static bool IsWildcardPattern(string patternToTest) =>
        patternToTest.Any(c => c is '*' or '?');

    public delegate bool Replacer(ReadOnlySpan<char> macroName, TextWriter writer);

    public static int ReplaceMacros(ReadOnlySpan<char> source, TextWriter writer, Replacer replacer)
    {
        var found = 0;

        var offset = 0;
        for (;;)
        {
            // find start of next macro, writing remainder if no more macros
            var begin = source.IndexOf("{{");
            if (begin < 0)
            {
                writer.Write(source);
                break;
            }

            // write what was before the macro and advance
            var oldSpan = source;
            writer.Write(source[..begin]);
            source = source[(begin+2)..];
            offset += begin+2;

            // find end of this macro
            var end = source.IndexOf("}}");
            if (end < 0)
                throw new FormatException($"Macro starting at offset {offset} and beginning with '{oldSpan.SliceSafe(0, 20).ToString()}' was not closed");

            // collect the macro name and advance
            var macro = source[..end];
            source = source[(end+2)..];
            offset += end+2;

            // find replacement matching the macro
            if (!replacer(macro, writer))
                throw new FormatException($"Unrecognized macro '{macro.ToString()}'");

            ++found;
        }

        return found;
    }

    public static bool ContainsMacros(ReadOnlySpan<char> source) =>
        source.IndexOf("{{") >= 0;

    public static string ReplaceMacros(string source, Replacer replacer)
    {
        if (!ContainsMacros(source))
            return source;

        var sb = new StringBuilder();
        return ReplaceMacros(source, new StringWriter(sb), replacer) == 0
            ? source
            : sb.ToString();
    }

    public static string ReplaceMacros(string source, params (string name, Action<TextWriter> replacer)[] replacements) =>
        ReplaceMacros(source, CreateMacroReplacer(replacements));

    public static Replacer CreateMacroReplacer(params (string name, Action<TextWriter> replacer)[] replacements) =>
        CreateMacroReplacer(10, replacements);

    public static Replacer CreateMacroReplacer(int useDictIfLengthAtLeast, params (string name, Action<TextWriter> replacer)[] replacements)
    {
        // $$$ TODO: validate that the macro names are valid, and that there are no duplicates
        // also if there is no end marker when move to support $macro type names, check for ambiguous/overlapping names
        // (for example they cannot contain '{{' or '}}' or be empty, or have "macro_a" and "macro_ab" as separate macros)

        // if it's small, do a linear search
        if (replacements.Length < useDictIfLengthAtLeast)
        {
            return (macroName, writer) =>
            {
                foreach (var (name, action) in replacements)
                {
                    if (!macroName.SequenceEqual(name))
                        continue;

                    action(writer);
                    return true;
                }

                return false;
            };
        }

        // bigger gets a dict, more setup time and alloc, but faster lookup
        var dict = replacements.ToDictionary();
        return (macroName, writer) =>
        {
            if (!dict.TryGetValue(macroName.ToString(), out var action))
                return false;

            action(writer);
            return true;
        };
    }
}
