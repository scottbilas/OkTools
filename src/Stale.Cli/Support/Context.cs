using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dumpify;
using Spectre.Console;
using Spectre.Console.Rendering;
using Vezel.Cathode;
using InvalidOperationException = System.InvalidOperationException;

#pragma warning disable CA1822

enum PerfStatType
{
    Init,
    Command,
}

class PerfStat
{
    DateTime? _start, _stop;

    public void Start()
    {
        if (_start != null)
            throw new InvalidOperationException("already started");
        _start = DateTime.Now;
    }

    public void Stop()
    {
        if (_start == null)
            throw new InvalidOperationException("not started");
        if (_stop != null)
            throw new InvalidOperationException("already stopped");
        _stop = DateTime.Now;
    }

    public DateTime StartTime => _start ?? throw new InvalidOperationException("did not start");
    public DateTime StopTime => _stop ?? throw new InvalidOperationException("did not stop");
    public TimeSpan Elapsed => StopTime - StartTime;
}

[PublicAPI]
class Context : IDisposable
{
    readonly CancellationTokenSource _cancelSource = new();
    readonly IDumpOutput _dumpOutput;

    public Context(VirtualTerminal? terminal = null)
    {
#       pragma warning disable RS0030
        Terminal = terminal ?? Vezel.Cathode.Terminal.System;
#       pragma warning restore RS0030

        LongTasks = new(_cancelSource.Token);
        _dumpOutput = new TerminalDumpOutput(Terminal);
    }

    public void Dispose()
    {
        _cancelSource.Dispose();

        var stillRunning = LongTasks.GetTasksSnapshot();
        if (stillRunning.Any(t => !t.Weak))
        {
            if (IsVerbose || Debugger.IsAttached)
            {
                ErrorLine("Long tasks were still running on exit:");
                for (var i = 0; i < stillRunning.Count; ++i)
                {
                    var task = stillRunning[i];
                    Error($"  [{i+1}/{stillRunning.Count}] \"{task.Name}\"");
                    if (task.Weak)
                        Error(" (weak)");
                    ErrorLine();
                    ErrorLine($"  serial={task.Serial}, id={task.Task.Id}, status={task.Task.Status}");
                    ErrorLine(task.Creation.ToString().Split('\n').Select(l => " " + l).StringJoin("\n"));
                }
            }
            else
            {
                ErrorLine("Long tasks were still running on exit: " + stillRunning
                    .Select(v => v.Name + (v.Weak ? " (weak)" : ""))
                    .StringJoin(", "));
            }
        }
    }

    public CancellationToken CancelToken => _cancelSource.Token;
    public bool IsCancellationRequested => _cancelSource.IsCancellationRequested;
    public void Cancel() => _cancelSource.Cancel();

    public VirtualTerminal Terminal { get; }
    public LongTasks LongTasks { get; }

    public StaleCliArguments Options = null!;
    public bool IsVerbose; // TODO: either make readonly for better JIT or force caller to check this before calling Verbose funcs

    public void VerboseLine(string text)
    {
        if (IsVerbose)
            OutMarkupInterpLine($"[grey]{text}[/]");
    }

    public void Verbose(string text)
    {
        if (IsVerbose)
            OutMarkupInterp($"[grey]{text}[/]");
    }

    // Out

    public       void      Out         (string text)                => Terminal.Out(text);
    public       void      Out         (ReadOnlySpan<char> span)    => Terminal.Out(span);
    public       void      Out         (IRenderable renderable)     => Out(renderable.ToAnsi());
    public       ValueTask OutAsync    (string text)                => Terminal.OutAsync(text, CancelToken);
    public       ValueTask OutAsync    (ReadOnlyMemory<char> chars) => Terminal.OutAsync(chars, CancelToken);
    public       ValueTask OutAsync    (IRenderable renderable)     => OutAsync(renderable.ToAnsi());

    public       void      OutLine     ()                           => Terminal.OutLine();
    public       void      OutLine     (string text)                => Terminal.OutLine(text);
    public       void      OutLine     (ReadOnlySpan<char> span)    { Out(span); OutLine(); } // no OutLine provided for ReadOnlySpan<char>
    public       void      OutLine     (IRenderable renderable)     => OutLine(renderable.ToAnsi());
    public       ValueTask OutLineAsync()                           => Terminal.OutLineAsync(CancelToken);
    public       ValueTask OutLineAsync(string text)                => Terminal.OutLineAsync(text, CancelToken);
    public async ValueTask OutLineAsync(ReadOnlyMemory<char> chars) { await OutAsync(chars); await OutLineAsync(); } // no OutLineAsync provided for ReadOnlyMemory<char>
    public       ValueTask OutLineAsync(IRenderable renderable)     => OutLineAsync(renderable.ToAnsi());

    // Markup

    public       void      Out                     (string text, Style style)                      => Out(new Text(text, style));
    public       ValueTask OutAsync                (string text, Style style)                      => OutAsync(new Text(text, style));
    public       void      OutLine                 (string text, Style style)                      => OutLine(new Text(text, style));
    public       ValueTask OutLineAsync            (string text, Style style)                      => OutLineAsync(new Text(text, style));
    public       void      OutMarkup               (string markup, Style? style = null)            => Out(new Markup(markup, style).ToAnsi());
    public       ValueTask OutMarkupAsync          (string markup, Style? style = null)            => OutAsync(new Markup(markup, style).ToAnsi());
    public       void      OutMarkupLine           (string markup, Style? style = null)            => OutLine(new Markup(markup, style).ToAnsi());
    public       ValueTask OutMarkupLineAsync      (string markup, Style? style = null)            => OutLineAsync(new Markup(markup, style).ToAnsi());
    public       void      OutMarkupInterp         (FormattableString markup, Style? style = null) => Out(Markup.FromInterpolated(markup, style).ToAnsi());
    public       ValueTask OutMarkupInterpAsync    (FormattableString markup, Style? style = null) => OutAsync(Markup.FromInterpolated(markup, style).ToAnsi());
    public       void      OutMarkupInterpLine     (FormattableString markup, Style? style = null) => OutLine(Markup.FromInterpolated(markup, style).ToAnsi());
    public       ValueTask OutMarkupInterpLineAsync(FormattableString markup, Style? style = null) => OutLineAsync(Markup.FromInterpolated(markup, style).ToAnsi());

    // Error

    public void      Error         (string value) => Terminal.Error(value);
    public ValueTask ErrorAsync    (string value) => Terminal.ErrorAsync(value, CancelToken);

    public void      ErrorLine     ()             => Terminal.ErrorLine();
    public void      ErrorLine     (string value) => Terminal.ErrorLine(value);
    public ValueTask ErrorLineAsync()             => Terminal.ErrorLineAsync(CancelToken);
    public ValueTask ErrorLineAsync(string value) => Terminal.ErrorLineAsync(value, CancelToken);

    public void Error(Exception exception, bool fullInfo) => Error(exception
        .GetRenderable(fullInfo
            ? ExceptionFormats.Default
            : ExceptionFormats.ShortenEverything)
        .ToAnsi());
    public void Error(Exception exception) => Error(exception, IsVerbose);

    // Verbose/Dump

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
        output: _dumpOutput,
        tableConfig: new() { ShowTableHeaders = false });

    // Other

    public PerfStat this[PerfStatType type] => _stats[(int)type];

    // Private

    class TerminalDumpOutput : IDumpOutput
    {
        public TerminalDumpOutput(VirtualTerminal terminal) =>
            TextWriter = terminal.StandardOut.TextWriter;

        // i'd like to adjust the config to have a MemberProvider that skips fields that are set to their default values
        // but that would break the table rendering, where all rows need the same fields. would require a deeper change
        // to dumpify to support that.

        public RendererConfig AdjustConfig(in RendererConfig config) => config;
        public TextWriter TextWriter { get; }
    }

    readonly PerfStat[] _stats = EnumUtility.GetNames<PerfStatType>().Select(_ => new PerfStat()).ToArray();

    static readonly ColorConfig k_verboseDumpColors = new(new DumpColor("#808080"));
}
