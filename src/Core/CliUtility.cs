using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OkTools.Core;

[PublicAPI]
public static class CliUtility
{
    public static IEnumerable<string> ParseCommandLineArgs(string commandLine) => Regex
        .Matches(commandLine, """(?<=^|\s)(?:"(?:\\.|[^"\\])*"?|[^\s"]+)""")
        .AsEnumerable()
        .Select(m =>
        {
            var arg = m.Value;

            // remove surrounding quotes if present
            if (arg.StartsWith('"') && arg.EndsWith('"'))
                arg = arg[1..^1];

            // unescape escaped quotes if any
            return arg.Replace("\\\"", "\"");
        });

    public static string CommandLineArgsToString(IEnumerable<string> args) => args
        .Select(a =>
        {
            if (a.IsNullOrWhiteSpace())
                return "";

            if (a.Contains(' ') && !(a[0] == '"' && a[^1] == '"'))
                return '"' + a.Replace("\"", "\\\"") + '"';

            return a.Replace("\"", "\\\"");
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

    public enum StdChannel
    {
        Stdout,
        Stderr
    }

    // TODO: use initializer struct
    public static int Execute(
        string exePath, IEnumerable<object>? processArgs, string? workingDirectory,
        Action<string, StdChannel>? onLine, IEnumerable<string>? stdinLines = null)
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
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
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
                    onLine(line.Data, StdChannel.Stdout);
            }
        };
        process.ErrorDataReceived += (_, line) =>
        {
            if (line.Data == null)
                stderrCompleted.Set();
            else if (onLine != null)
            {
                lock (serializer)
                    onLine(line.Data, StdChannel.Stderr);
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
            (line, stream) => (stream == StdChannel.Stdout ? stdout : stderr).Add(line),
            stdin);
    }
}
