using System.Text;
using System.Text.RegularExpressions;

namespace OkTools.Core;

[PublicAPI]
public static partial class TextUtility
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

    public static string WildcardToRegexText(string wildcardPattern) =>
        $"^{InnerWildcardToRegexText(wildcardPattern)}$";
    public static string WildcardToRegexText(IEnumerable<string> wildcardPatterns)
    {
        var text = $"^(?:{wildcardPatterns.Select(InnerWildcardToRegexText).StringJoin('|')})$";
        if (text == "^(?:)$")
            throw new ArgumentException("Empty wildcard pattern list is ambiguous", nameof(wildcardPatterns));
        return text;
    }

    public static Regex WildcardToRegex(string wildcardPattern, RegexOptions rxOptions = RegexOptions.IgnoreCase) =>
        new(WildcardToRegexText(wildcardPattern), rxOptions);
    public static Regex WildcardToRegex(IEnumerable<string> wildcardPatterns, RegexOptions rxOptions = RegexOptions.IgnoreCase) =>
        new(WildcardToRegexText(wildcardPatterns), rxOptions);

    public static bool IsWildcardPattern(string patternToTest) =>
        patternToTest.Any(c => c is '*' or '?');

    static string InnerWildcardToRegexText(string wildcardPattern)
    {
        if (wildcardPattern.Length == 0)
            throw new ArgumentException("Empty wildcard pattern is ambiguous", nameof(wildcardPattern));

        return Regex.Escape(wildcardPattern).Replace(@"\*", ".*").Replace(@"\?", ".");
    }
}
