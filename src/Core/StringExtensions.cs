using System.Text;
using System.Text.RegularExpressions;

namespace OkTools.Core;

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

        for (var (i, iend) = (startIndex, startIndex + count); i != iend; ++i)
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

    public static IEnumerable<string> SelectToStrings<T>(this IEnumerable<T> @this) =>
        @this.Select(v => v?.ToString()).WhereNotNull();

    public static string StringJoin<T>(this IEnumerable<T> @this, string separator) =>
        string.Join(separator, @this);
    public static string StringJoin<T>(this IEnumerable<T> @this, char separator) =>
        string.Join(separator, @this);
    public static string StringJoin<T>(this IEnumerable<T> @this) =>
        string.Join("", @this);

    public static string RegexReplace(this string @this, string pattern, string replacement) =>
        Regex.Replace(@this, pattern, replacement);
    public static string RegexReplace(this string @this, string pattern, string replacement, RegexOptions options) =>
        Regex.Replace(@this, pattern, replacement, options);
    public static string RegexReplace(this string @this, string pattern, string replacement, RegexOptions options, TimeSpan matchTimeout) =>
        Regex.Replace(@this, pattern, replacement, options, matchTimeout);
    public static string RegexReplace(this string @this, string pattern, MatchEvaluator evaluator) =>
        Regex.Replace(@this, pattern, evaluator);
    public static string RegexReplace(this string @this, string pattern, MatchEvaluator evaluator, RegexOptions options) =>
        Regex.Replace(@this, pattern, evaluator, options);
    public static string RegexReplace(this string @this, string pattern, MatchEvaluator evaluator, RegexOptions options, TimeSpan matchTimeout) =>
        Regex.Replace(@this, pattern, evaluator, options, matchTimeout);

    public static IEnumerable<string> SelectToLower(this IEnumerable<string> @this) =>
        @this.Select(s => s.ToLowerInvariant());
    public static IEnumerable<string> SelectToToUpper(this IEnumerable<string> @this) =>
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

        // more early-out and a bit silly scenarios, but why not..
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
}
