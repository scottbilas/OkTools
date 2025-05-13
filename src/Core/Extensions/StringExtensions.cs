using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace OkTools.Core.Extensions;

[PublicAPI]
public static class StringExtensions
{
    [ContractAnnotation("null=>true", true), Pure]
    public static bool IsNullOrEmpty(this string? @this) => string.IsNullOrEmpty(@this);
    [ContractAnnotation("null=>true", true), Pure]
    public static bool IsNullOrWhiteSpace(this string? @this) => string.IsNullOrWhiteSpace(@this);

    public static bool IsEmpty(this string @this) => @this.Length == 0;
    public static bool Any(this string @this) => @this.Length != 0;

    public static bool EqualsIgnoreCase(this string @this, string other) => @this.Equals(other, StringComparison.OrdinalIgnoreCase);

    public static int IndexOfNot(this string @this, char value, int startIndex, int count)
    {
        // TODO: use span/unsafe and optimize a bit (see string.IndexOf for example)

        if (startIndex < 0 || startIndex > @this.Length)
            throw new ArgumentOutOfRangeException(nameof(startIndex), $"Out of range 0 <= {startIndex} <= {@this.Length}");
        if (count < 0 || count > @this.Length - startIndex)
            throw new ArgumentOutOfRangeException(nameof(count), $"Out of range 0 <= {count} <= {@this.Length - startIndex}");

        for (var (i, ie) = (startIndex, startIndex + count); i != ie; ++i)
        {
            if (@this[i] != value)
                return i;
        }

        return -1;
    }
    public static int IndexOfNot(this string @this, char value, int startIndex) =>
        @this.IndexOfNot(value, startIndex, @this.Length - startIndex);
    public static int IndexOfNot(this string @this, char value) =>
        @this.IndexOfNot(value, 0, @this.Length);

    // note: we follow the good SpanHelpers.LastIndexOf() convention (span starts at start) rather than the bad string.LastIndexOf convention (span ends at start+1)
    public static int LastIndexOfNot(this string @this, char value, int startIndex, int count)
    {
        // TODO: use span/unsafe and optimize a bit (see string.IndexOf for example)

        if (startIndex < 0 || startIndex > @this.Length)
            throw new ArgumentOutOfRangeException(nameof(startIndex), $"Out of range 0 <= {startIndex} <= {@this.Length}");
        if (count < 0 || count > @this.Length - startIndex)
            throw new ArgumentOutOfRangeException(nameof(count), $"Out of range 0 <= {count} <= {@this.Length - startIndex}");

        for (var i = startIndex + count - 1; i >= startIndex; --i)
        {
            if (@this[i] != value)
                return i;
        }

        return -1;
    }
    public static int LastIndexOfNot(this string @this, char value, int startIndex) =>
        @this.LastIndexOfNot(value, startIndex, @this.Length - startIndex);
    public static int LastIndexOfNot(this string @this, char value) =>
        @this.LastIndexOfNot(value, 0, @this.Length);

    // left/mid/right are BASIC-inspired names, and never throw except for a clear programming error
    // TODO: replace with Span

    public static string Left(this string @this, int maxChars) =>
        @this[..Math.Min(maxChars, @this.Length)];

    // TODO: get rid of -1 special code
    public static string Mid(this string @this, int offset, int maxChars = -1)
    {
        if (offset < 0)
            throw new ArgumentException("offset must be >= 0", nameof(offset));

        var safeOffset = Math.Min(offset, @this.Length);
        var safeEnd = maxChars >= 0 ? Math.Min(safeOffset + maxChars, @this.Length) : @this.Length;

        return @this[safeOffset..safeEnd];
    }

    public static string Right(this string @this, int maxChars) =>
        @this[^Math.Min(maxChars, @this.Length)..];

    public static string Truncate(this string @this, int maxChars, string trailer = "...")
    {
        if (@this.Length <= maxChars)
            return @this;

        return @this.Left(maxChars - trailer.Length) + trailer;
    }

    public static ReadOnlySpan<char> AsSpanSafe(this string @this, int start, int maxLength)
    {
        if (start < 0)
            throw new ArgumentException("offset must be >= 0", nameof(start));

        var safeStart = Math.Min(start, @this.Length);
        var safeEnd = Math.Min(safeStart + maxLength, @this.Length);

        return @this.AsSpan(safeStart, safeEnd - safeStart);
    }

    public static ReadOnlySpan<char> AsSpanSafe(this string @this, int start) =>
        @this.AsSpanSafe(start, @this.Length);

    public static StringSegment AsStringSegment(this string @this) =>
        new(@this);

    public static IEnumerable<string> SelectToStrings<T>(this IEnumerable<T> @this) =>
        @this.Select(v => v?.ToString()).WhereNotNull();

    public static string[] SplitTrimRemoveEmpty(this string @this, string split) => @this
#       if NETSTANDARD
        .Split(split).Select(p => p.Trim()).Where(p => p != "").ToArray();
#       else
        .Split(split, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
#       endif

    public static string[] SplitTrimRemoveEmpty(this string @this, char split) => @this
#       if NETSTANDARD
        .Split(split).Select(p => p.Trim()).Where(p => p != "").ToArray();
#       else
        .Split(split, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
#       endif

    public static string[] SplitTrim(this string @this, string split) => @this
#       if NETSTANDARD
        .Split(split).Select(p => p.Trim()).ToArray();
#       else
        .Split(split, StringSplitOptions.TrimEntries);
#       endif

    public static string[] SplitTrim(this string @this, char split) => @this
#       if NETSTANDARD
        .Split(split).Select(p => p.Trim()).ToArray();
#       else
        .Split(split, StringSplitOptions.TrimEntries);
#       endif

    static string ToStringIfNeeded(string original, ReadOnlySpan<char> span) =>
        span.Length == original.Length ? original : span.ToString();

    public static string Trim(this string @this, int startIndex) =>
        ToStringIfNeeded(@this, @this.AsSpan(startIndex).Trim());
    public static string Trim(this string @this, char trimChar, int startIndex) =>
        ToStringIfNeeded(@this, @this.AsSpan(startIndex).Trim(trimChar));
    public static string Trim(this string @this, ReadOnlySpan<char> trimChars, int startIndex) =>
        ToStringIfNeeded(@this, @this.AsSpan(startIndex).Trim(trimChars));

    public static string Trim(this string @this, int startIndex, int length) =>
        ToStringIfNeeded(@this, @this.AsSpan(startIndex, length).Trim());
    public static string Trim(this string @this, char trimChar, int startIndex, int length) =>
        ToStringIfNeeded(@this, @this.AsSpan(startIndex, length).Trim(trimChar));
    public static string Trim(this string @this, ReadOnlySpan<char> trimChars, int startIndex, int length) =>
        ToStringIfNeeded(@this, @this.AsSpan(startIndex, length).Trim(trimChars));

    public static string StringJoin<T>(this IEnumerable<T> @this, string separator) =>
        string.Join(separator, @this);
    public static string StringJoin<T>(this IEnumerable<T> @this, char separator) =>
        string.Join(separator, @this);
    public static string StringJoin<T>(this IEnumerable<T> @this) =>
        string.Join("", @this);

    public static string StringJoin(this ITuple @this, string separator) =>
        string.Join(separator, @this.SelectObjects());
    public static string StringJoin(this ITuple @this, char separator) =>
        string.Join(separator, @this.SelectObjects());
    public static string StringJoin(this ITuple @this) =>
        string.Join("", @this.SelectObjects());

    public static IEnumerable<string> SelectLines(this string @this, bool trimLines = false, bool skipEmptyLines = false)
    {
        var reader = new StringReader(@this);
        while (reader.ReadLine() is { } line)
        {
            if (trimLines)
                line = line.Trim();
            if (skipEmptyLines && line.IsEmpty())
                continue;

            yield return line;
        }
    }

    public static IEnumerable<StringSegment> SelectLinesAsSegments(this StringSegment @this, bool trimLines = false)
    {
        StringSegment DoTrim(StringSegment segment) => trimLines ? segment.Trim() : segment;

        for (var iter = @this;;)
        {
            var end = iter.IndexOf('\n');
            if (end < 0)
            {
                if (iter.Any)
                    yield return DoTrim(iter);
                yield break;
            }

            var trim = 0;
            if (end > 0 && iter[end-1] == '\r')
                trim = 1;
            yield return DoTrim(iter[..(end-trim)]);

            iter = iter[(end+1)..];
        }
    }

    public static IEnumerable<StringSegment> SelectLinesAsSegments(this string @this, bool trimLines = false) =>
        SelectLinesAsSegments(@this.AsStringSegment(), trimLines);

    public static bool WildcardMatch(this string @this, string wildcardPattern, RegexOptions rxOptions) =>
        TextUtility.WildcardToRegex(wildcardPattern, rxOptions).IsMatch(@this);
    public static bool WildcardMatch(this string @this, string wildcardPattern) =>
        TextUtility.WildcardToRegex(wildcardPattern).IsMatch(@this);

    public static Match RegexMatch(this string @this, string rxPattern) =>
        Regex.Match(@this, rxPattern);
    public static Match RegexMatch(this string @this, string rxPattern, RegexOptions rxOptions) =>
        Regex.Match(@this, rxPattern, rxOptions);
    public static Match RegexMatch(this string @this, Regex rx) =>
        rx.Match(@this);

    public static IReadOnlyList<Match> RegexMatches(this string @this, string rxPattern) =>
        Regex.Matches(@this, rxPattern);
    public static IReadOnlyList<Match> RegexMatches(this string @this, string rxPattern, RegexOptions rxOptions) =>
        Regex.Matches(@this, rxPattern, rxOptions);
    public static IReadOnlyList<Match> RegexMatches(this string @this, Regex rx) =>
        rx.Matches(@this);

    public static string[] RegexSplit(this string @this, string rxPattern) =>
        Regex.Split(@this, rxPattern);
    public static string[] RegexSplit(this string @this, string rxPattern, RegexOptions options) =>
        Regex.Split(@this, rxPattern, options);

    public static string[] RegexSplit(this string @this, Regex rx) =>
        rx.Split(@this);
    public static string[] RegexSplit(this string @this, Regex rx, int count) =>
        rx.Split(@this, count);
    public static string[] RegexSplit(this string @this, Regex rx, int count, int startAt) =>
        rx.Split(@this, count, startAt);

    public static string RegexReplace(this string @this, string rxPattern, string replacement) =>
        Regex.Replace(@this, rxPattern, replacement);
    public static string RegexReplace(this string @this, string rxPattern, string replacement, RegexOptions options) =>
        Regex.Replace(@this, rxPattern, replacement, options);
    public static string RegexReplace(this string @this, string rxPattern, string replacement, RegexOptions options, TimeSpan matchTimeout) =>
        Regex.Replace(@this, rxPattern, replacement, options, matchTimeout);
    public static string RegexReplace(this string @this, string rxPattern, MatchEvaluator evaluator) =>
        Regex.Replace(@this, rxPattern, evaluator);
    public static string RegexReplace(this string @this, string rxPattern, MatchEvaluator evaluator, RegexOptions options) =>
        Regex.Replace(@this, rxPattern, evaluator, options);
    public static string RegexReplace(this string @this, string rxPattern, MatchEvaluator evaluator, RegexOptions options, TimeSpan matchTimeout) =>
        Regex.Replace(@this, rxPattern, evaluator, options, matchTimeout);
    public static string RegexReplace(this string @this, Regex rx, string replacement) =>
        rx.Replace(@this, replacement);
    public static string RegexReplace(this string @this, Regex rx, MatchEvaluator evaluator) =>
        rx.Replace(@this, evaluator);

    public static string ToLowerFirstChar(this string @this)
    {
        if (@this.Length == 0)
            return @this;
        if (!char.IsUpper(@this[0]))
            return @this;

        return @this[0].ToLower() + @this[1..];
    }

    public static string ToUpperFirstChar(this string @this)
    {
        if (@this.Length == 0)
            return @this;
        if (!char.IsLower(@this[0]))
            return @this;

        return @this[0].ToUpper() + @this[1..];
    }

    public static IEnumerable<string> SelectToLower(this IEnumerable<string> @this) =>
        @this.Select(s => s.ToLowerInvariant());
    public static IEnumerable<string> SelectToUpper(this IEnumerable<string> @this) =>
        @this.Select(s => s.ToUpperInvariant());

    // the buffer is for avoiding the builder alloc each time. useful when processing multiple lines, and can cut allocs by half.
    public static string ExpandTabs(this string @this, int tabWidth, StringBuilder? buffer = null)
    {
        if (tabWidth < 0)
            throw new ArgumentException("tabWidth must be >= 0", nameof(tabWidth));

        var tabCount = @this.Count(c => c == '\t');

        // early out if nothing to do
        if (tabCount == 0)
            return @this;

        // more early-out and a bit silly scenarios, but why not...
        if (tabWidth == 0)
            return @this.Replace("\t", "");
        if (tabWidth == 1)
            return @this.Replace('\t', ' ');

        var capacity = @this.Length + tabCount * (tabWidth - 1);
        if (buffer != null)
            buffer.EnsureCapacity(capacity);
        else
            buffer = new StringBuilder(capacity);

        foreach (var c in @this)
        {
            if (c != '\t')
                buffer.Append(c);
            else
                buffer.Append(' ', tabWidth - buffer.Length % tabWidth);
        }

        var expanded = buffer.ToString();
        buffer.Clear();
        return expanded;
    }

    static readonly char[] k_escapeThese = [' ', '\"', '\n', '\t'];

    public static string SimpleEscape(this string @this)
    {
        if (@this.Length == 0)
            return "\"\"";
        if (@this.IndexOfAny(k_escapeThese) < 0)
            return @this;

        var sb = new StringBuilder();
        sb.Append('"');
        foreach (var c in @this)
        {
            switch (c)
            {
                case '\"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n");  break;
                case '\t': sb.Append("\\t");  break;
                default  : sb.Append(c);      break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
