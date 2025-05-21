using System.Text.RegularExpressions;

namespace OkTools.Core.Extensions;

public static partial class StringExtensions
{
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

    public static (StringSegment a, StringSegment b) Split2(this StringSegment @this, char separator)
    {
        var idx = @this.IndexOf(separator);
        if (idx < 0)
            throw new ArgumentException($"String '{@this}' does not contain separator '{separator}'");

        return (@this[..idx], @this[(idx + 1)..]);
    }

    public static (StringSegment a, StringSegment b, StringSegment c) Split3(this StringSegment @this, char separator)
    {
        var (a, bx) = Split2(@this, separator);
        var (b, c ) = Split2(bx, separator);
        return (a, b, c);
    }

    public static (StringSegment a, StringSegment b, StringSegment c, StringSegment d) Split4(this StringSegment @this, char separator)
    {
        var (a, bx) = Split2(@this, separator);
        var (b, cx) = Split2(bx, separator);
        var (c, d ) = Split2(cx, separator);
        return (a, b, c, d);
    }

    public static (StringSegment a, StringSegment b, StringSegment c, StringSegment d, StringSegment e) Split5(this StringSegment @this, char separator)
    {
        var (a, bx) = Split2(@this, separator);
        var (b, cx) = Split2(bx, separator);
        var (c, dx) = Split2(cx, separator);
        var (d, e ) = Split2(dx, separator);
        return (a, b, c, d, e);
    }

    public static (StringSegment a, StringSegment b) Split2(this string @this, char separator) => Split2(@this.AsStringSegment(), separator);
    public static (StringSegment a, StringSegment b, StringSegment c) Split3(this string @this, char separator) => Split3(@this.AsStringSegment(), separator);
    public static (StringSegment a, StringSegment b, StringSegment c, StringSegment d) Split4(this string @this, char separator) => Split4(@this.AsStringSegment(), separator);
    public static (StringSegment a, StringSegment b, StringSegment c, StringSegment d, StringSegment e) Split5(this string @this, char separator) => Split5(@this.AsStringSegment(), separator);

    public static (T1 a, T2 b) SplitParse2<T1, T2>(this string @this, char separator)
    {
        var (a, b) = @this.Split2(separator);
        return (
            (T1)Convert.ChangeType(a.ToString(), typeof(T1)),
            (T2)Convert.ChangeType(b.ToString(), typeof(T2)));
    }

    public static (T1 a, T2 b, T3 c) SplitParse3<T1, T2, T3>(this string @this, char separator)
    {
        var (a, b, c) = @this.Split3(separator);
        return (
            (T1)Convert.ChangeType(a.ToString(), typeof(T1)),
            (T2)Convert.ChangeType(b.ToString(), typeof(T2)),
            (T3)Convert.ChangeType(c.ToString(), typeof(T3)));
    }

    public static (T1 a, T2 b, T3 c, T4 d) SplitParse4<T1, T2, T3, T4>(this string @this, char separator)
    {
        var (a, b, c, d) = @this.Split4(separator);
        return (
            (T1)Convert.ChangeType(a.ToString(), typeof(T1)),
            (T2)Convert.ChangeType(b.ToString(), typeof(T2)),
            (T3)Convert.ChangeType(c.ToString(), typeof(T3)),
            (T4)Convert.ChangeType(d.ToString(), typeof(T4)));
    }

    public static (T1 a, T2 b, T3 c, T4 d, T5 e) SplitParse5<T1, T2, T3, T4, T5>(this string @this, char separator)
    {
        var (a, b, c, d, e) = @this.Split5(separator);
        return (
            (T1)Convert.ChangeType(a.ToString(), typeof(T1)),
            (T2)Convert.ChangeType(b.ToString(), typeof(T2)),
            (T3)Convert.ChangeType(c.ToString(), typeof(T3)),
            (T4)Convert.ChangeType(d.ToString(), typeof(T4)),
            (T5)Convert.ChangeType(e.ToString(), typeof(T5)));
    }
}
