using Spectre.Console;
using Spectre.Console.Advanced;
using Spectre.Console.Rendering;

static class TerminalExtensions
{
    public static string ToAnsi(this IRenderable @this) =>
        AnsiConsole.Console.ToAnsi(@this);
}
