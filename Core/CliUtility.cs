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

    class Line
    {
        string _text;
        int _start, _end;

        public Line(string text) => _text = text;

        public int Start
        {
            get => _start;
            set
            {
                if (value < 0 || value > End)
                    throw new Exception($"Line.Start: {value} < {0} || {value} > {End} (old Start: {_start})");
                _start = value;
            }
        }

        public int End
        {
            get => _end;
            set
            {
                if (value < Start || value > _text.Length)
                    throw new Exception($"Line.End: {value} < {Start} || {value} > {_text.Length} (old End: {_end})");
                _end = value;
            }
        }

        public int FindLine(int newStart)
        {
            // find end of this line
            var end = _text.IndexOf('\n', _start = newStart);

            // find actual end of this line
            _end = end < 0 ? _text.Length : end;
            _end = FindTrimEnd(_end);

            // go to next line (or stay at end)
            return end < 0 ? end : end+1;
        }

        public void TrimStart()
        {
            while (_end > _start && _text[_start] == ' ')
                ++_start;
        }

        public int FindTrimEnd(int end)
        {
            while (end > _start && char.IsWhiteSpace(_text[end-1]))
                --end;
            return end;
        }

        public int Length => End - Start;

        public override string ToString()
        {
            var text = _text.Substring(_start, Length)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            return $"'{text}' (start={Start}, end={End}, len={Length})";
        }
    }

    static readonly Regex k_TableRx = new(@"^ *[-*] |\b {2,}\b", RegexOptions.Multiline);

    // wrapWidth: defaults to console width
    // minWrapWidth: if a soft wrap takes the line width below this amount, then do a hard wrap instead
    public static string Reflow(string text, int wrapWidth, int minWrapWidth = 0, string eol = "\n")
    {
        if (text.Contains('\t'))
            throw new ArgumentException("Tab characters not ok in a reflow string");

        var sb = new StringBuilder();
        var currentLine = new Line(text);

        var (lastLen, lastIndent, nextStart) = (0, 0, 0);

        while (nextStart >= 0)
        {
            nextStart = currentLine.FindLine(nextStart);
            if (currentLine.Length == 0)
            {
                sb.Append(eol);
                continue;
            }

            var indent = text.IndexOfNot(' ', currentLine.Start, currentLine.Length);
            currentLine.Start = indent;
            var interruptedIndent = indent != lastIndent;
            lastIndent = indent;

            void Append(int len, bool terminate)
            {
                if (len > 0)
                {
                    if (lastLen > 0)
                    {
                        // if we were extending the previous line, add a space
                        sb.Append(' ');
                        ++lastLen;
                    }
                    else if (indent > 0)
                    {
                        // it's the start of a new line, so indent
                        sb.Append(' ', indent);
                        lastLen += indent;
                    }

                    // add line text
                    var end = currentLine.FindTrimEnd(currentLine.Start + len);
                    sb.Append(text, currentLine.Start, end - currentLine.Start);
                    currentLine.Start += len;
                    lastLen += len;

                    // trim any following spaces so any further continuation wraps cleanly
                    currentLine.TrimStart();
                }

                if (terminate)
                {
                    sb.Append(eol);
                    lastLen = 0;
                }
            }

            for (var available = wrapWidth - indent - lastLen - (lastLen > 0 ? 1 : 0);
                 currentLine.Length > available; available = wrapWidth)
            {
                var wrapBreak = text.LastIndexOf(' ', currentLine.Start + available, available + 1);
                if (wrapBreak >= currentLine.Start + minWrapWidth)
                    Append(wrapBreak - currentLine.Start, true); // ok to wrap here
                else
                    Append(available, true); // too narrow, do a hard break
            }

            // write any remainder
            Append(currentLine.Length, false);
        }

        return sb.ToString();
    }
}
