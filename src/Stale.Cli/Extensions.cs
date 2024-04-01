using Dumpify;
using Spectre.Console;
using Spectre.Console.Advanced;
using Spectre.Console.Rendering;
using Vezel.Cathode;
using Vezel.Cathode.IO;

static class Extensions
{
    static readonly TerminalOutput s_terminalOutput = new();

    class TerminalOutput : IDumpOutput
    {
        // i'd like to adjust the config to have a MemberProvider that skips fields that are set to their default values
        // but that would break the table rendering, where all rows need the same fields. would require a deeper change
        // to dumpify to support that.

        public RendererConfig AdjustConfig(in RendererConfig config) => config;
        public TextWriter TextWriter { get; } = Terminal.TerminalOut.ToTextWriter();
    }

    public static void DumpTerminal<T>(this T @this, string label) => @this.Dump(
        label: $"'{label}'",
        useDescriptors: false,
        typeNames: new() { ShowTypeNames = false },
        colors: ColorConfig.DefaultColors,
        output: s_terminalOutput,
        tableConfig: new() { ShowTableHeaders = false });

    public static string ToAnsi(this IRenderable @this) =>
        AnsiConsole.Console.ToAnsi(@this);

    public static void TerminalOut(this IRenderable @this) =>
        Terminal.Out(AnsiConsole.Console.ToAnsi(@this));

    public static void TerminalOut(this Exception @this, bool fullInfo = false) => @this
        .GetRenderable(fullInfo
            ? ExceptionFormats.Default
            : ExceptionFormats.ShortenTypes | ExceptionFormats.ShortenPaths)
        .TerminalOut();

    public static TextWriter ToTextWriter(this TerminalWriter @this) =>
        new StreamWriter(new TerminalOutputStream(@this), Terminal.Encoding) { AutoFlush = true };
}
