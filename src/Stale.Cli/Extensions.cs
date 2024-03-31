using Dumpify;
using Spectre.Console;
using Spectre.Console.Advanced;
using Spectre.Console.Rendering;
using Vezel.Cathode;
using Vezel.Cathode.IO;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Console;
using Win32_PInvoke = Windows.Win32.PInvoke;

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

    // see https://github.com/vezel-dev/cathode/issues/156
    public static void HackFixNewlineOptions(this SystemVirtualTerminal @this)
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
            throw new NotSupportedException("Requires at least Windows 5.1.2600"); // uh, this is winxp.. TODO: find a way to set this globally where it won't break on linux

        using var handle = Win32_PInvoke.CreateFile(
            "CONOUT$",
            (uint)(GENERIC_ACCESS_RIGHTS.GENERIC_READ | GENERIC_ACCESS_RIGHTS.GENERIC_WRITE),
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            0,
            null);

        Win32_PInvoke.GetConsoleMode(handle, out var consoleMode);
        consoleMode &= ~CONSOLE_MODE.DISABLE_NEWLINE_AUTO_RETURN;
        Win32_PInvoke.SetConsoleMode(handle, consoleMode);
    }
}
