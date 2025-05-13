using System.Text;

namespace OkTools.Core;

using SimpleReplacer = (string Macro, Action<TextWriter> WriteAction);
using SimpleReplacement = (string Macro, string Replacement);

public static partial class TextUtility
{
    // return true to say "handled"
    public delegate bool MacroReplacer(StringSegment macroName, TextWriter writer);

    public static StringSegment ReplaceMacros(StringSegment source, SimpleReplacer[] replacers) =>
        ReplaceMacros(source, CreateMacroReplacer(replacers));
    public static StringSegment ReplaceMacros(StringSegment source, SimpleReplacement[] replacements) =>
        ReplaceMacros(source, CreateMacroReplacer(replacements));
    public static StringSegment ReplaceMacros(StringSegment source, MacroReplacer replacer)
    {
        if (source.IndexOf("{{") >= 0)
        {
            var sb = new StringBuilder();
            if (ReplaceMacros(source, new StringWriter(sb), replacer) != 0)
                return sb.ToString();
        }

        return source;
    }

    public static int ReplaceMacros(StringSegment source, TextWriter writer, MacroReplacer replacer)
    {
        var found = 0;

        var offset = 0;
        for (;;)
        {
            // find start of next macro, writing remainder if no more macros
            var begin = source.IndexOf("{{");
            if (begin < 0)
            {
                writer.Write(source.Span);
                break;
            }

            // write what was before the macro and advance
            var oldSpan = source;
            writer.Write(source[..begin].Span);
            source = source[(begin+2)..];
            offset += begin+2;

            // find end of this macro
            var end = source.IndexOf("}}");
            if (end < 0)
                throw new FormatException($"Macro starting at offset {offset} and beginning with '{oldSpan.SliceSafe(0, 20)}' was not closed");

            // collect the macro name and advance
            var macro = source[..end];
            source = source[(end+2)..];
            offset += end+2;

            // find replacement matching the macro
            if (!replacer(macro, writer))
                throw new FormatException($"Unrecognized macro '{macro}'");

            ++found;
        }

        return found;
    }

#   if NETSTANDARD
    static readonly char[] k_invalidMacroChars = "{}\t\r\n".AsSpan().ToArray();
    public static bool IsValidMacroName(StringSegment macroName) =>
        !macroName.IsEmpty && macroName.String.IndexOfAny(k_invalidMacroChars, macroName.SegmentStart, macroName.Length) < 0;
#   else
    static readonly SearchValues<char> k_invalidMacroChars = SearchValues.Create("{}\t\r\n");
    public static bool IsValidMacroName(StringSegment macroName) =>
        !macroName.IsEmpty && !macroName.Span.ContainsAny(k_invalidMacroChars);
#   endif

    public static MacroReplacer CreateMacroReplacer(IEnumerable<SimpleReplacement> replacements) => CreateMacroReplacer(
        replacements.Select(r => (r.Macro, r.Replacement)), (writer, replacement) => writer.Write(replacement));
    public static MacroReplacer CreateMacroReplacer(IEnumerable<SimpleReplacer> replacers) => CreateMacroReplacer(
        replacers.Select(r => (r.Macro, r.WriteAction)), (writer, action) => action(writer));

    public static MacroReplacer CreateMacroReplacer<TUserData>(
        IEnumerable<(string name, TUserData userData)> macros,
        Action<TextWriter, TUserData> applyAction)
    {
        var dict = macros.ToDictionary();

        if (dict.Keys.Any(k => !IsValidMacroName(k)))
            throw new ArgumentException("Invalid macro name");

        return (macro, writer) =>
        {
            if (!dict.TryGetValue(macro, out var userData))
                return false;

            applyAction(writer, userData);
            return true;
        };
    }
}
