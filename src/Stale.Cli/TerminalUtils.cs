using Spectre.Console;
using Spectre.Console.Advanced;
using Spectre.Console.Rendering;
using Vezel.Cathode;
using Vezel.Cathode.IO;

static class TerminalExtensions
{
    public static string ToAnsi(this IRenderable @this) =>
        AnsiConsole.Console.ToAnsi(@this);

    public static TextWriter ToTextWriter(this TerminalWriter @this) =>
        new StreamWriter(new TerminalOutputStream(@this), Terminal.Encoding) { AutoFlush = true };
}
