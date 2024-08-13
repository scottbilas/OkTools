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

class Context : IDisposable
{
    readonly CancellationTokenSource _cancelSource = new();
    readonly LongTasks _longTasks;

    public Context()
    {
        _longTasks = new(_cancelSource.Token);
    }

    public void Dispose()
    {
        _cancelSource.Dispose();

        var stillRunning = _longTasks.GetTasksSnapshot();
        if (stillRunning.Any())
        {
            if (IsVerbose || Debugger.IsAttached)
            {
                ErrorLine("Long tasks were still running on exit:");
                for (var i = 0; i < stillRunning.Count; ++i)
                {
                    var task = stillRunning[i];
                    ErrorLine($"  [{i+1}/{stillRunning.Count}] \"{task.Name}\"");
                    ErrorLine($"  serial={task.Serial}, id={task.Task.Id}, status={task.Task.Status}");
                    ErrorLine(task.Creation.ToString().Split('\n').Select(l => " " + l).StringJoin("\n"));
                }
            }
            else
                ErrorLine("Long tasks were still running on exit: " + stillRunning.Select(v => v.Name).StringJoin(", "));
        }
    }

    public CancellationToken CancelToken => _cancelSource.Token;
    public bool IsCancellationRequested => _cancelSource.IsCancellationRequested;
    public void Cancel() => _cancelSource.Cancel();

    public LongTasks LongTasks => _longTasks;

    public StaleCliArguments Options = null!;
    public bool IsVerbose; // TODO: either make readonly for better JIT or force caller to check this before calling Verbose funcs

    public void VerboseLine(string text)
    {
        if (IsVerbose)
            OutMarkupLineInterp($"[grey]{text}[/]");
    }

    public void Verbose(string text)
    {
        if (IsVerbose)
            OutMarkupInterp($"[grey]{text}[/]");
    }

    [Conditional("DEBUG")]
    public void DebugLine(string text)
    {
        var frames = new StackTrace(1)
            .GetFrames()
            .Reverse()
            .SelectWhere(frame =>
            {
                var name = MiscUtils.ToNiceMethodName(frame.GetMethod()!);

                // "just my code" only
                return (name, !name.StartsWith("System."));
            });

        Debug.WriteLine(
            $"{Environment.CurrentManagedThreadId,2}: "+
            $"#{Task.CurrentId ?? '-',2} | "+
            $"{MiscUtils.SequenceDuplicatesAsDots(frames).StringJoin(" ↗ ")} ⦚ {text}");
    }

    public void OutLine() =>
        Terminal.OutLine();

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
        OutLine(new Markup(text).ToAnsi());
    public void OutMarkup(string text) =>
        Out(new Markup(text).ToAnsi());
    public void OutMarkupLineInterp(FormattableString value) =>
        OutLine(Markup.FromInterpolated(value).ToAnsi());
    public void OutMarkupInterp(FormattableString value) =>
        Out(Markup.FromInterpolated(value).ToAnsi());

    public void ErrorLine<T>(T value) =>
        Terminal.ErrorLine(value);
    public void ErrorLine() =>
        Terminal.ErrorLine();
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

    public PerfStat this[PerfStatType type] => _stats[(int)type];

    class TerminalDumpOutput : IDumpOutput
    {
        // i'd like to adjust the config to have a MemberProvider that skips fields that are set to their default values
        // but that would break the table rendering, where all rows need the same fields. would require a deeper change
        // to dumpify to support that.

        public RendererConfig AdjustConfig(in RendererConfig config) => config;
        public TextWriter TextWriter { get; } = Terminal.StandardOut.TextWriter;
    }

    readonly PerfStat[] _stats = EnumUtility.GetNames<PerfStatType>().Select(_ => new PerfStat()).ToArray();

    static readonly IDumpOutput s_dumpOutput = new TerminalDumpOutput();
    static readonly ColorConfig k_verboseDumpColors = new(new DumpColor("#808080"));
}
