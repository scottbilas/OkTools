using System.Runtime.CompilerServices;
using Dumpify;
using Spectre.Console;
using Spectre.Console.Rendering;
using Vezel.Cathode;

#pragma warning disable CA1822

class Context : IDisposable
{
    readonly CancellationTokenSource _cancelSource = new();

    public Context()
    {
        _ansiConsole = new TerminalAnsiConsole(this);
    }

    public void Dispose()
    {
        _cancelSource.Dispose();
    }

    public CancellationToken CancelToken => _cancelSource.Token;
    public bool IsCancellationRequested => _cancelSource.IsCancellationRequested;
    public void Cancel() => _cancelSource.Cancel();

    public StaleCliArguments Options = null!;
    public bool IsVerbose;

    public void VerboseLine(string text)
    {
        if (IsVerbose)
            _ansiConsole.MarkupLineInterpolated($"[grey]{text}[/]");
    }

    public void Verbose(string text)
    {
        if (IsVerbose)
            _ansiConsole.MarkupInterpolated($"[grey]{text}[/]");
    }

    public void OutLine() =>
        Terminal.OutLine();

    public void OutLine<T>(T value) =>
        Terminal.OutLine(value);
    public void Out<T>(T value) =>
        Terminal.Out(value);

    public void OutLine(ReadOnlySpan<char> span)
    {
        Terminal.Out(span); // no OutLine provided for ReadOnlySpan<char>
        Terminal.OutLine();
    }
    public void Out(ReadOnlySpan<char> span) =>
        Terminal.Out(span);

    public void OutLine(IRenderable renderable) =>
        Terminal.OutLine(renderable.ToAnsi());
    public void Out(IRenderable renderable) =>
        Terminal.Out(renderable.ToAnsi());

    public void OutMarkupLine(string text) =>
        _ansiConsole.MarkupLine(text!);
    public void OutMarkup(string text) =>
        _ansiConsole.Markup(text);
    public void OutMarkupLineInterp(FormattableString value) =>
        _ansiConsole.MarkupLineInterpolated(value);
    public void OutMarkupInterp(FormattableString value) =>
        _ansiConsole.MarkupInterpolated(value);

    public void ErrorLine<T>(T value) =>
        Terminal.ErrorLine(value);
    public void Error<T>(T value) =>
        Terminal.Error(value);

    public void Error(Exception exception, bool fullInfo) => Error(exception
        .GetRenderable(fullInfo
            ? ExceptionFormats.Default
            : ExceptionFormats.ShortenEverything)
        .ToAnsi());
    public void Error(Exception exception) => Error(exception, IsVerbose);

    public void VerboseDump<T>(T value, [CallerArgumentExpression(nameof(value))] string? label = null)
    {
        if (IsVerbose)
            OutDump(value, label, k_verboseDumpColors);
    }

    public void OutDump<T>(T value, [CallerArgumentExpression(nameof(value))] string? label = null) => OutDump(
        value, label, ColorConfig.DefaultColors);

    void OutDump<T>(T value, string? label, ColorConfig colors) => value.Dump(
        label: $"'{label ?? throw new ArgumentNullException(nameof(label), "unexpected unnamed parameter")}'",
        useDescriptors: false,
        typeNames: new() { ShowTypeNames = false },
        colors: colors,
        output: s_dumpOutput,
        tableConfig: new() { ShowTableHeaders = false });

    class TerminalAnsiConsole(Context ctx) : IAnsiConsole
    {
        // we only need Write()
        public Profile Profile => throw new InvalidOperationException();
        public IAnsiConsoleCursor Cursor => throw new InvalidOperationException();
        public IAnsiConsoleInput Input => throw new InvalidOperationException();
        public IExclusivityMode ExclusivityMode => throw new InvalidOperationException();
        public RenderPipeline Pipeline => throw new InvalidOperationException();
        public void Clear(bool home) => throw new InvalidOperationException();

        public void Write(IRenderable renderable)
        {
            ctx.Out(renderable);
        }
    }

    class TerminalDumpOutput : IDumpOutput
    {
        // i'd like to adjust the config to have a MemberProvider that skips fields that are set to their default values
        // but that would break the table rendering, where all rows need the same fields. would require a deeper change
        // to dumpify to support that.

        public RendererConfig AdjustConfig(in RendererConfig config) => config;
        public TextWriter TextWriter { get; } = Terminal.StandardOut.TextWriter;
    }

    readonly IAnsiConsole _ansiConsole;

    static readonly IDumpOutput s_dumpOutput = new TerminalDumpOutput();
    static readonly ColorConfig k_verboseDumpColors = new(new DumpColor("#808080"));
}
