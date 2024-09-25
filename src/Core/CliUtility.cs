using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OkTools.Core;

[PublicAPI]
public static class CliUtility
{
    #if NETSTANDARD
    #pragma warning disable CA1839
    public static string ProgramName => Process.GetCurrentProcess().MainModule!.FileName.ToNPath().FileNameWithoutExtension;
    #pragma warning restore CA1839
    #else
    public static string ProgramName => Path.GetFileNameWithoutExtension(Environment.ProcessPath!);
    #endif

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
}
