using System.Diagnostics;
using System.Reflection;
using System.Text;

public static class DebugDump
{
    public static string ToEscapedString(this string @this)
    {
        var count = 0;
        for (var i = 0; i != @this.Length; ++i)
        {
            switch (@this[i])
            {
                case '\n':
                case '\r':
                case '\t':
                    ++count;
                    break;
            }
        }

        var buf = new char[@this.Length + count];
        for (var (i, o) = (0, 0); i != @this.Length; ++i, ++o)
        {
            switch (@this[i])
            {
                case '\n':
                    buf[o++] = '\\';
                    buf[o] = 'n';
                    break;
                case '\r':
                    buf[o++] = '\\';
                    buf[o] = 'r';
                    break;
                case '\t':
                    buf[o++] = '\\';
                    buf[o] = 't';
                    break;
                default:
                    buf[o] = @this[i];
                    break;
            }
        }

        return new string(buf);
    }

    public static string ToArgString(this string? @this) =>
        @this != null ? $"\"{@this.ToEscapedString()}\"" : "null";

    const int k_indentMultiple = 2;

    // TODO(TRIM): consider something like https://github.com/byme8/Apparatus.AOT.Reflection

    public static string ToDumpString(this object @this, string? name = null, bool quiet = true, Func<PropertyInfo, bool>? filter = null, int? wrapWidth = null, int maxWrapLines = 0)
    {
        // i hate this function impl, but i'm not going to let that stop me from making it worse!

        var sb = new StringBuilder();
        var indent = 0;

        if (name != null)
        {
            sb.AppendLine($"['{name}' {@this.GetType().FullName}]");
            ++indent;
        }

        var properties = @this
            .GetType()
            .GetProperties()
            .Where(pi => filter?.Invoke(pi) ?? true)
            .OrderBy(pi => pi.Name)
            .Select(pi =>
            {
                string? text = null;

                var value = pi.GetValue(@this);
                switch (value)
                {
                    case null:
                        if (!quiet)
                            text = "null";
                        break;
                    case bool b:
                        if (b || !quiet)
                            text = b.ToString().ToLower();
                        break;
                    case IntPtr p:
                        if (p != default || !quiet)
                            text = $"0x{p:x}";
                        break;
                    case string str:
                        text = str.ToArgString();
                        break;
                    case IEnumerable<string> enumerable:
                        text = '[' + enumerable.Select(s => s.ToArgString()).StringJoin(", ") + ']';
                        break;
                    case IEnumerable<KeyValuePair<string, string?>> stringDict:
                        if (@this is ProcessStartInfo && pi.Name == "Environment")
                        {
                            stringDict = stringDict
                                .OrderBy(kv => Environment.GetEnvironmentVariable(kv.Key) != kv.Value ? -1 : 1) // inherited env vars go at the bottom
                                .ThenBy(kv => kv.Key);
                        }
                        else
                            stringDict = stringDict.OrderBy(kv => kv.Key);
                        text = '[' + stringDict.Select(kv => $"{kv.Key}={kv.Value.ToArgString()}").StringJoin(", ") + ']';
                        break;
                    case NPath npath:
                        text = npath.ToString(SlashMode.Native);
                        break;
                    default:
                        text = value.ToString();
                        break;
                }

                return (name: pi.Name, text);
            })
            .Where(p => p.text != null)
            .ToArray();

            var maxNameLen = properties.Max(p => p.name.Length);

            foreach (var p in properties)
            {
                var text = p.text!;

                var wrapIndent = sb.Length;
                sb.Append(' ', indent * k_indentMultiple);
                sb.Append(p.name);
                sb.Append(' ', maxNameLen - p.name.Length);
                sb.Append(" = ");
                wrapIndent = sb.Length - wrapIndent;

                if (wrapWidth != null && wrapWidth > wrapIndent + 20) // 20 just a reasonable "any less than this and wrapping looks way worse than not"
                {
                    var written = 0;
                    var wrappedLines = 0;

                    while (written < text.Length)
                    {
                        if (written > 0)
                            sb.Append(' ', wrapIndent);

                        var end = Math.Min(text.Length, written + wrapWidth.Value - wrapIndent);
                        sb.AppendLine(text[written..end]);
                        written += end - written;
                        ++wrappedLines;

                        if (maxWrapLines > 0 && wrappedLines >= maxWrapLines)
                        {
                            sb.Append(' ', wrapIndent);
                            sb.AppendLine("(...remainder truncated...)");
                            break;
                        }
                    }
                }
                else
                    sb.AppendLine(text);
            }

        return sb.ToString();
    }
}
