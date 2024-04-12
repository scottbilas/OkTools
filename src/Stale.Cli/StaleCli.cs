using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using DocoptNet;
using Spectre.Console;
using Vezel.Cathode;
using Vezel.Cathode.Processes;

const string programVersion = "0.1";
const int jsonVersion = 1;

var programStart = DateTime.Now;
using var ctx = new Context();
ctx[StatType.Init].Start();
var logStats = false;

// ReSharper disable AccessToDisposedClosure
// ^ talking about ctx here, it's ok, it outlives everything

Terminal.Signaled += signalContext =>
{
    if (signalContext.Signal != TerminalSignal.Interrupt)
        return;

    // TODO: pass through ctrl-c and attempt to let the child process exit gracefully
    // (may also need to handle ctrl-break, ctrl-close, etc)
    // (may also need to pass y/n confirmation thingy)

    ctx.Cancel();
};

try
{
    {
        var isVerbose = false;
        NPath? useCwd = null;

        // TODO: fix up this pre-config stuff, it's brittle, inconsistent error reporting (doesn't benefit from docopt parser checker)
        for (var i = 0; i < args.Length; ++i)
        {
            if (args[i] == "--verbose")
                isVerbose = true;
            else if (args[i] == "--stats")
                logStats = true;
            else if (args[i] == "--cwd")
            {
                if (++i == args.Length)
                    throw new DocoptInputErrorException("--cwd needs a path");
                useCwd = args[i];
            }
            else
            {
                args = args[i..];
                break;
            }
        }

        if (isVerbose)
        {
            ctx.IsVerbose = true;
            ctx.VerboseLine("Enabling verbose mode");
        }

        if (logStats && ctx.IsVerbose)
            ctx.VerboseLine("Enabling stats printout at end");

        if (useCwd != null)
        {
            if (ctx.IsVerbose)
                ctx.VerboseLine($"Setting cwd to: {useCwd}");
            if (!useCwd.DirectoryExists())
                throw new DocoptInputErrorException($"--cwd path does not exist: {useCwd}");
            NPath.SetCurrentDirectory(useCwd);
        }

        var (exitCode, options) = StaleCliArguments.CreateParser().Parse(
            args, programVersion, StaleCliArguments.Help, StaleCliArguments.Usage,
            outWriter: Terminal.StandardOut.TextWriter,
            errWriter: Terminal.StandardError.TextWriter,
            wrapWidth: Terminal.Size.Width);
        if (exitCode != null)
            return (int)exitCode.Value;

        ctx.Options = options;
    }

    if (ctx.IsVerbose)
        ctx.VerboseDump(ctx.Options);

    if (ctx.Options.CmdRecord)
        return Run(() => Record(ctx.Options.ArgRecorded, ctx.Options.ArgCommand!, [..ctx.Options.ArgArg]));

    if (ctx.Options.CmdPlay)
    {
        double? ratio = null;
        int? delay = null;

        if (ctx.Options.OptSpeed != null)
        {
            var success = false;

            var m = Regex.Match(ctx.Options.OptSpeed, @"(?<ratio>[0-9.]+)x|(?<delay>\d+)ms");
            var ratioGroup = m.Groups["ratio"];
            var delayGroup = m.Groups["delay"];

            if (ratioGroup.Success)
            {
                if (double.TryParse(ratioGroup.Value, out var value) && value > 0)
                {
                    ratio = value;
                    success = true;
                }
            }
            else if (delayGroup.Success)
            {
                if (int.TryParse(delayGroup.Value, out var value) && value >= 0)
                {
                    delay = value;
                    success = true;
                }
            }

            if (!success)
            {
                throw new CliErrorException(
                    CliExitCode.ErrorUsage,
                    "Unable to parse speed argument; needs to be a ratio like 1.23x or a delay like 456ms");
            }
        }

        if (ctx.IsVerbose)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (ratio != null && ratio != 1.0)
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at {ratio}x speed");
            else if (delay == 0)
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at maximum speed");
            else if (delay != null)
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at {delay}ms per line");
            else
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at original speed");
        }

        return Run(() => Play(ctx.Options.ArgRecorded!, ratio, delay, ctx.CancelToken));
    }

    return Run(() => Main(ctx.Options.ArgCommand!, [..ctx.Options.ArgArg]));
}
catch (CliErrorException x)
{
    ctx.ErrorLine(x.Message);
    return (int)x.Code;
}
catch (Exception x)
{
    ctx.Error(x);
    return (int)CliExitCode.ErrorSoftware;
}

int Run(Func<Task<CliExitCode>> task)
{
    ctx[StatType.Init].Stop();

    ctx[StatType.Command].Start();
    var result = task().Result;
    ctx[StatType.Command].Stop();

    if (logStats)
    {
        ctx.OutMarkupLine("[aqua]-- Stats from run --[/]");
        var table = new Table() { Border = TableBorder.Minimal };
        table.AddColumn("Stat");
        table.AddColumn("Start");
        table.AddColumn("Stop");
        table.AddColumn("Elapsed");

        foreach (var statType in EnumUtility.GetValues<StatType>())
        {
            var stat = ctx[statType];
            table.AddRow(
                statType.ToString(),
                stat.StartTime.ToString("hh:mm:ss.fff"),
                stat.StopTime.ToString("hh:mm:ss.fff"),
                stat.Elapsed.TotalSeconds.ToString("F3"));
        }

        var totalStart = ctx[EnumUtility.GetValues<StatType>().First()].StartTime;
        var totalStop = ctx[EnumUtility.GetValues<StatType>().Last()].StopTime;
        table.AddRow(
            "Total",
            totalStart.ToString("hh:mm:ss.fff"),
            totalStop.ToString("hh:mm:ss.fff"),
            (totalStop - totalStart).TotalSeconds.ToString("F3"));

        ctx.Out(table);

/*        var operationElapsed = DateTime.Now - operationStart;
        var programElapsed   = DateTime.Now - programStart;
        ctx.OutLine(
            $"Finished in {operationElapsed.TotalSeconds:F3}s (total {programElapsed.TotalSeconds:F3}s) "+
            $"with exit code {result} ({(int)result})");*/
    }

    return (int)result;
}

async Task<CliExitCode> Main(string command, IReadOnlyList<string> args)
{
    var (process, captures) = ShellExec(command, args);

    var dims = Terminal.Size;
    Terminal.Resized += size => dims = size; // TODO: also do a re-layout

    const string statusColor = "bold yellow on navyblue";
    const string stderrColor = "white on darkred";

    var status = $">{process.Id} $ {command} {CliUtility.CommandLineArgsToString(args)}";
    ctx.OutMarkupLine(
        status.Length <= dims.Width
        ? $"[{statusColor}]{status.PadRight(dims.Width).EscapeMarkup()}[/]"
        : $"[{statusColor}]{status[..(dims.Width-1)].EscapeMarkup()}[/][blue]»[/]");

    await foreach (var capture in captures.ReadAllAsync(ctx.CancelToken))
    {
        void Process()
        {
            var span = capture.Line.AsSpan();

            void Out(ReadOnlySpan<char> span)
            {
                // TODO: should i be using async versions of these Out funcs..?

                if (capture.IsStdErr)
                    ctx.OutMarkupLine($"[{stderrColor}]{span.ToString().PadRight(dims.Width).EscapeMarkup()}[/]");
                else
                    ctx.OutLine(span);
            }

            // this would be a blank line, not "no line"
            if (span.Length == 0)
            {
                Out(span);
                return;
            }

            while (span.Length > dims.Width)
            {
                Out(span[..dims.Width]);
                span = span[dims.Width..];
            }

            if (span.Length > 0)
                Out(span);
        }

        Process();
    }

    return CliExitCode.Success;
}

async Task<CliExitCode> Record(string? recordedPath, string command, IReadOnlyList<string> args)
{
    var (process, captures) = ShellExec(command, args);

    if (ctx.IsVerbose)
    {
        ctx.VerboseLine($">{process.Id} $ {command} {CliUtility.CommandLineArgsToString(args)}");
        ctx.VerboseLine($" (Recording to ${ctx.Options.ArgRecorded ?? "stdout"})");
    }

    var jsonStream = recordedPath != null
        ? File.Create(recordedPath)
        : Terminal.StandardOut.Stream;

    await using var writer = new StreamWriter(jsonStream);
    writer.AutoFlush = true;

    await using var json = new Utf8JsonWriter(jsonStream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    json.WriteStartObject();
    json.WriteNumber("version", jsonVersion);
    json.WriteStartArray("captures");
    json.Flush();
    writer.Write('\n');

    await foreach (var capture in captures.ReadAllAsync(ctx.CancelToken))
    {
        writer.Write('\n');

        json.WriteStartObject();
        if (capture.IsStdErr)
            json.WriteBoolean("isStdErr", true);
        json.WriteString("when", capture.When);
        json.WriteString("line", capture.Line);
        json.WriteEndObject();
        json.Flush();
    }

    json.WriteEndArray();
    json.Flush();
    writer.Write("\n\n");

    json.WriteNumber("exitcode", process.Completion.Result);

    json.WriteEndObject();
    json.Flush();

    writer.Write('\n');

    return CliExitCode.Success;
}

async Task<CliExitCode> Play(string recordedPath, double? ratio, int? delay, CancellationToken cancel)
{
    using var reader = new StreamReader(File.OpenRead(recordedPath));

    var json = await JsonDocument.ParseAsync(reader.BaseStream, default, cancel);

    var version = json.RootElement.GetProperty("version").GetInt32();
    if (version != jsonVersion)
        throw new CliErrorException(CliExitCode.ErrorDataErr, $"Recorded file '{recordedPath}' is version {{version}}, expected {{jsonVersion}}");

    var captures = json.RootElement.GetProperty("captures");
    var exitCode = json.RootElement.GetProperty("exitcode").GetInt32();

    if (ratio == null && delay == null)
        ratio = 1;

    DateTime? last = null;

    foreach (var capture in captures.EnumerateArray())
    {
        var isStdErr = capture.TryGetProperty("isStdErr", out var isStdErrEl) && isStdErrEl.GetBoolean();
        var when = capture.GetProperty("when").GetDateTime();
        var line = capture.GetProperty("line").GetString();

        if (ratio != null)
        {
            if (last != null)
            {
                var delta = (when - last.Value).TotalMilliseconds;
                await Task.Delay((int)(delta / ratio.Value), cancel); // returns CompletedTask if delay (int ms) is 0
            }
            last = when;
        }
        else if (delay != 0)
            await Task.Delay(delay!.Value, cancel);

        if (isStdErr)
            Terminal.StandardError.WriteLine(line);
        else
            Terminal.StandardOut.WriteLine(line);
    }

    return (CliExitCode)exitCode;
}

(ChildProcess process, ChannelReader<Capture> reader) ShellExec(NPath command, IReadOnlyList<string> args)
{
    if (ShellExecUtility.ConfigureProcessExitToAlsoKillChildProcesses() && ctx.IsVerbose)
        ctx.VerboseLine("Configuring OS to kill child processes if the current process exits");

    // TODO: consider checking if it's a console app
    // (see IsWindowsApplication at PowerShell\src\System.Management.Automation\engine\NativeCommandProcessor.cs:1199)

    var (commandPath, extraArgs) = ShellExecUtility.ResolveShellCommand(command);
    if (extraArgs.Any())
        args = [..extraArgs, ..args];

    var process = new ChildProcessBuilder()
        .WithFileName(commandPath)
        .WithArguments(args)
        .WithRedirections(false, true, true)
        .WithCreateWindow(false)
        .WithWindowStyle(ProcessWindowStyle.Hidden)
        .WithCancellationToken(ctx.CancelToken)
        .WithThrowOnError(false)
        .Run();

    var captures = Channel.CreateUnbounded<Capture>(new UnboundedChannelOptions { SingleReader = true });

    var open = 0;

    async void Write(TextReader reader, bool isStdErr)
    {
        Interlocked.Increment(ref open);

        while (!ctx.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ctx.CancelToken);
            if (line == null)
            {
                if (Interlocked.Decrement(ref open) == 0)
                    captures.Writer.Complete();
                break;
            }

            await captures.Writer.WriteAsync(new Capture(isStdErr, DateTime.Now, line), ctx.CancelToken);
        }
    }

    _ = Task.Run(() => Write(process.StandardOut.TextReader, false), ctx.CancelToken);
    _ = Task.Run(() => Write(process.StandardError.TextReader, true), ctx.CancelToken);

    return (process, captures.Reader);
}

readonly record struct Capture(bool IsStdErr, DateTime When, string Line)
{
    public override string ToString() => $"{(IsStdErr ? "! " : "")}{Line.SimpleEscape()} ({When:g})";
}
