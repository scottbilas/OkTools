using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace OkTools.Core;

[PublicAPI]
public static class CliUtility
{
    public static IEnumerable<string> ParseCommandLineArgs(string commandLine) => Regex
        .Matches(commandLine, @"""(?<c>[^""]+)""|(?<c>[^\s""]+)")
        .Select(r => r.Groups["c"].Value);

    public static string CommandLineArgsToString(IEnumerable<string> args) => args
        .Select(a =>
        {
            if (a.IsNullOrWhiteSpace())
                return "";
            if (a.Contains(' '))
                return '"' + a + '"';
            return a;
        })
        .StringJoin(" ")
        .Trim();

    public static IEnumerable<string> SelectStdinLines()
    {
        for (;;)
        {
            var line = Console.ReadLine();
            if (line == null)
                yield break;

            yield return line;
        }
    }

    public enum StdStream
    {
        Stdout,
        Stderr
    }

    // TODO: use initializer struct
    public static int Execute(
        string exePath, IEnumerable<object>? processArgs, string? workingDirectory,
        Action<string, StdStream>? onLine, IEnumerable<string>? stdinLines = null)
    {
        processArgs = processArgs.OrEmpty();

        var processArgsText = processArgs
            .Select(obj =>
            {
                var str = obj.ToString();
                if (str?.Contains(' ') == true)
                    str = '"' + str + '"';
                return str;
            })
            .StringJoin(" ");

        using var stdoutCompleted = new ManualResetEvent(false);
        using var stderrCompleted = new ManualResetEvent(false);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                // keep new process completely out of user view
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                ErrorDialog = false,

                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                FileName = exePath,
                Arguments = processArgsText,

                RedirectStandardInput = stdinLines != null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        // avoid caller needing to do this (and pretty much everybody will want it)
        var serializer = new object();

        // ReSharper disable AccessToDisposedClosure
        // ^ this is ok because we either kill or wait for process to stop before `using` will dispose the events
        process.OutputDataReceived += (_, line) =>
        {
            if (line.Data == null)
                stdoutCompleted.Set();
            else if (onLine != null)
            {
                lock (serializer)
                    onLine(line.Data, StdStream.Stdout);
            }
        };
        process.ErrorDataReceived += (_, line) =>
        {
            if (line.Data == null)
                stderrCompleted.Set();
            else if (onLine != null)
            {
                lock (serializer)
                    onLine(line.Data, StdStream.Stderr);
            }
        };
        // ReSharper restore AccessToDisposedClosure

        // start everything
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // write if needed
        if (stdinLines != null)
        {
            foreach (var line in stdinLines)
                process.StandardInput.WriteLine(line);

            process.StandardInput.Close();
        }

        // wait for proc and all reads to finish
        process.WaitForExit();
        stdoutCompleted.WaitOne();
        stderrCompleted.WaitOne();

        return process.ExitCode;
    }

    public static int Execute(
        string exePath, IEnumerable<object>? processArgs, string workingDirectory,
        ICollection<string> stdout, ICollection<string> stderr, IEnumerable<string>? stdin = null)
    {
        return Execute(
            exePath, processArgs, workingDirectory,
            (line, stream) => (stream == StdStream.Stdout ? stdout : stderr).Add(line),
            stdin);
    }

    public readonly struct StringSpan
    {
        public readonly string Text;
        public readonly int Start, End;

        public StringSpan(string text, int start, int end)
        {
            if (start < 0 || start > text.Length)
                throw new IndexOutOfRangeException(nameof(start) + $" out of range 0 <= {start} <= {text.Length}");
            if (end < start || end > text.Length)
                throw new IndexOutOfRangeException(nameof(end) + $" out of range {start} <= {end} <= {text.Length}");

            Text = text;
            Start = start;
            End = end;
        }

        public int Length => End - Start;
        public bool IsEmpty => Start == End;
        public bool Any => Start != End;

        public static StringSpan Empty => new("", 0, 0);

        public int TrimStartIndex()
        {
            var i = Start;
            for (; i != End; ++i)
            {
                if (!char.IsWhiteSpace(Text[i]))
                    break;
            }

            return i;
        }

        public StringSpan TrimStart() => new(Text, TrimStartIndex(), End);

        public int TrimEndIndex()
        {
            var i = End;
            for (; i > Start; --i)
            {
                if (!char.IsWhiteSpace(Text[i-1]))
                    break;
            }

            return i;
        }

        public StringSpan TrimEnd() => new(Text, Start, TrimEndIndex());

        public Match Match(Regex regex) => regex.Match(Text, Start, Length);

        public override string ToString()
        {
            var text = Text
                .Substring(Start, Length)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            return $"'{text}' (start={Start}, end={End}, len={Length})";
        }
    }

    public readonly struct LineSpan
    {
        public readonly StringSpan Span;
        public readonly int Indent;

        readonly bool _hasPrefix;

        public LineSpan(StringSpan span)
        {
            Span = span.TrimEnd();

            var indentMatch = Span.Match(s_indentRx);
            if (indentMatch.Success)
                Indent = indentMatch.Index - Span.Start + indentMatch.Length;
            else
                Indent = Span.TrimStartIndex() - Span.Start;

            _hasPrefix = Span.TrimStartIndex() < Indent;
        }

        public bool CanFlowInto(LineSpan other) => Span.Any && other.Span.Any && Indent == other.Indent && !other._hasPrefix;

        public override string ToString() => $"{Span}; indent={Indent}, prefix={_hasPrefix}";
    }

    static readonly Regex s_indentRx = new(@"^ *[-*] |\b {2,}\b");

    public static IEnumerable<LineSpan> SelectSpans(string text)
    {
        for (var start = 0; start != text.Length;)
        {
            var end = text.IndexOf('\n', start);

            var next = end;
            if (end < 0)
                end = next = text.Length;
            else
                ++next;

            yield return new LineSpan(new StringSpan(text, start, end));
            start = next;
        }
    }

    public static IEnumerable<LineSpan> JoinSpans

    public static string Reflow(string text, int wrapWidth, int minWrapWidth = 0, string eol = "\n")
    {
        if (wrapWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(wrapWidth), $" out of range 0 < {wrapWidth}");
        if (minWrapWidth < 0 || minWrapWidth >= wrapWidth)
            throw new ArgumentOutOfRangeException(nameof(minWrapWidth), $" out of range 0 <= {minWrapWidth} < {wrapWidth}");

        var sb = new StringBuilder();
        var mainLine = default(LineSpan);
        var currentLen = 0;

        void Newline()
        {
            sb.Append(eol);
            currentLen = 0;
        }

        foreach (var line in SelectSpans(text))
        {
            var span = line.Span;

            // detect if we're continuing a section or not
            if (mainLine.CanFlowInto(line))
                span = span.TrimStart();
            else
            {
                mainLine = line;

                if (currentLen != 0)
                {
                    // terminate previous in-progress line
                    sb.Append(eol);
                    currentLen = 0;
                }
            }

            if (span.IsEmpty)
            {
                // blank line
                Newline();
                continue;
            }

            do
            {
                var available = wrapWidth - currentLen;

                if (currentLen > 0)
                {
                    // continuing a previous line, so we will start out with a space before the next word
                    --available;
                }
                else if (line.Span.Start != mainLine.Span.Start)
                {
                    // it's a new line, but we're continuing a section, so start the line with an indent
                    sb.Append(' ', mainLine.Indent);
                    available -= mainLine.Indent;
                }

                var nextSpan = StringSpan.Empty;
                if (span.Length > available)
                {
                    // find nice place to break
                    var wrapBreak = span.Text.LastIndexOf(' ', span.Start + available, available + 1);

                    // ok, no we found a nice place to break, but we're past the minimum break point so fine to just
                    // finish this line (if it was in progress) and try again
                    if (wrapBreak < 0 && currentLen > 0 && currentLen >= minWrapWidth)
                    {
                        Newline();
                        continue;
                    }

                    // if the last wrap still occurs too early, we'll have to switch to a hard break
                    if (wrapBreak < span.Start + minWrapWidth)
                        wrapBreak = span.Start + available;

                    // take a chunk and advance
                    nextSpan = new StringSpan(span.Text, wrapBreak, span.End).TrimStart();
                    span = new StringSpan(span.Text, span.Start, wrapBreak).TrimEnd();
                }

                if (span.Any)
                {
                    if (currentLen > 0)
                        sb.Append(' ');

                    // now we can add the span that fits
                    sb.Append(span.Text, span.Start, span.Length);
                    currentLen += span.Length;
                }

                // newline if we know the span is going to continue
                if (nextSpan.Any)
                    Newline();

                span = nextSpan;
            }
            while (span.Length > 0);
        }

        return sb.ToString();
    }
}
